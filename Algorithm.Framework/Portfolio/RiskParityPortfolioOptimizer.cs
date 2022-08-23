/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Linq;
using Accord.Math;
using Accord.Statistics;

namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Provides an implementation of a risk parity portfolio optimizer that calculate the optimal weights
    /// with the weight range from 0 to 1 and equalize the risk carried by each asset
    /// </summary>
    public class RiskParityPortfolioOptimizer : IPortfolioOptimizer
    {
        private double _lower;
        private double _upper;

        /// <summary>
        /// Initialize a new instance of <see cref="RiskParityPortfolioOptimizer"/>
        /// </summary>
        /// <param name="lower">The lower bounds on portfolio weights</param>
        /// <param name="upper">The upper bounds on portfolio weights</param>
        public RiskParityPortfolioOptimizer(double lower = 1e-05, double upper =  Double.PositiveInfinity)
        {
            _lower = lower < 1e-05 ? 1e-05 : lower;     // has to be greater than or equal to 0
            _upper = upper < lower ? lower : _upper;
        }

        /// <summary>
        /// Perform portfolio optimization for a provided matrix of historical returns and an array of expected returns
        /// </summary>
        /// <param name="historicalReturns">Matrix of annualized historical returns where each column represents a security and each row returns for the given date/time (size: K x N).</param>
        /// <param name="expectedReturns">Unused.</param>
        /// <param name="covariance">Multi-dimensional array of double with the portfolio covariance of annualized returns (size: K x K).</param>
        /// <returns>Array of double with the portfolio weights (size: K x 1)</returns>
        public double[] Optimize(double[,] historicalReturns, double[] expectedReturns = null, double[,] covariance = null)
        {
            covariance = covariance ?? historicalReturns.Covariance();
            var size = covariance.GetLength(0);

            // Optimization Problem
            // minimize_{x >= 0} f(x) = 1/2 * x^T.S.x - b^T.log(x)
            // b = 1 / num_of_assets (equal budget of risk)
            // df(x)/dx = S.x - b / x
            // H(x) = S + Diag(b / x^2)
            var budget = Vector.Create(size, 1d / size);
            Func<double[], double> objective = (x) => 0.5 * Matrix.Dot(Matrix.Dot(x, covariance), x) - Matrix.Dot(budget, Elementwise.Log(x));
            Func<double[], double[]> gradient = (x) => Elementwise.Subtract(Matrix.Dot(covariance, x), Elementwise.Divide(budget, x));
            Func<double[], double[,]> hessian = (x) => Elementwise.Add(covariance, Matrix.Diagonal(Elementwise.Divide(budget, Elementwise.Multiply(x, x))));
            var solution = NonNegNewtonMethodOptimization(size, objective, gradient, hessian);
            
            // Normalize weights: w = x / x^T.1
            return Elementwise.Divide(solution, solution.Sum());
        }

        /// <summary>
        /// Newton method of minimization
        /// </summary>
        /// <param name="numOfVar">The number of variables (size of weight vector).</param>
        /// <param name="objective">The objective function.</param>
        /// <param name="gradient">Gradient of the objective function.</param>
        /// <param name="hessian">Hessian matrix of the objective function.</param>
        /// <param name="tolerance">Tolerance level of objective difference with previous steps to accept minimization result.</param>
        /// <param name="maxIter">Maximum iteration per optimization.</param>
        /// <returns>Array of double of argumented minimization</returns>
        public double[] NonNegNewtonMethodOptimization(int numOfVar, 
                                                       Func<double[], double> objective, 
                                                       Func<double[], double[]> gradient, 
                                                       Func<double[], double[,]> hessian, 
                                                       double tolerance = 1e-05,
                                                       int maxIter = 1000)
        {
            var weight = Vector.Create(numOfVar, 1d / numOfVar);
            var newObjective = Double.MinValue;
            var oldObjective = Double.MaxValue;
            var iter = 0;

            while (Math.Abs(newObjective - oldObjective) > tolerance && iter < maxIter)
            {
                // Store old objective value
                oldObjective = newObjective;

                // Get parameters for Newton method gradient descend
                var invHess = Matrix.Inverse(hessian(weight));
                var jacobian = gradient(weight);

                // Get next weight vector
                // x^{k + 1} = x^{k} - H^{-1}(x^{k}).df(x^{k}))
                weight = Elementwise.Subtract(weight, Matrix.Dot(invHess, jacobian));
                // Make sure the vector is within range
                weight = weight.Select(x => Math.Max(Math.Min(x, _upper), _lower)).ToArray();

                // Store new objective value
                newObjective = objective(weight);

                iter++;
            }

            return weight;
        }
    }
}