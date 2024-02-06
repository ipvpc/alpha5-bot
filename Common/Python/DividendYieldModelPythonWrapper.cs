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
using Python.Runtime;
using QuantConnect.Data;

namespace QuantConnect.Python
{
    /// <summary>
    /// Wraps a <see cref="PyObject"/> object that represents a dividend yield model
    /// </summary>
    public class DividendYieldModelPythonWrapper : IDividendYieldModel
    {
        private readonly dynamic _model;

        /// <summary>
        /// Constructor for initializing the <see cref="DividendYieldModelPythonWrapper"/> class with wrapped <see cref="PyObject"/> object
        /// </summary>
        /// <param name="model">Represents a security's model of dividend yield</param>
        public DividendYieldModelPythonWrapper(PyObject model)
        {
            _model = model.ValidateImplementationOf<IDividendYieldModel>();
        }

        /// <summary>
        /// Get dividend yield by a given date
        /// </summary>
        /// <param name="date">The date</param>
        /// <returns>Dividend yield on the given date</returns>
        public decimal GetDividendYield(DateTime date)
        {
            using var _ = Py.GIL();
            return (_model.GetDividendYield(date) as PyObject).GetAndDispose<decimal>();
        }

        /// <summary>
        /// Converts a <see cref="PyObject"/> object into a <see cref="IDividendYieldModel"/> object, wrapping it if necessary
        /// </summary>
        /// <param name="model">The Python model</param>
        /// <returns>The converted <see cref="IDividendYieldModel"/> instance</returns>
        public static IDividendYieldModel FromPyObject(PyObject model)
        {
            if (!model.TryConvert(out IDividendYieldModel dividendYieldModel))
            {
                dividendYieldModel = new DividendYieldModelPythonWrapper(model);
            }

            return dividendYieldModel;
        }
    }
}
