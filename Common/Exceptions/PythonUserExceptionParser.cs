﻿/*
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

using Python.Runtime;
using QuantConnect.Interfaces;
using QuantConnect.Scheduling;
using System;
using System.Collections.Generic;

namespace QuantConnect.Exceptions
{
    /// <summary>
    /// Parser that converts a <see cref="PythonException"/> thrown by a python algorithm into an <see cref="Exception"/>
    /// </summary>
    public class PythonUserExceptionParser : IExceptionParser
    {
        private const int _offset = 37;

        private static readonly Dictionary<string, string> _commonErrors = new Dictionary<string, string>
        {
            { "InvalidTokenError", "Trying to include an invalid token/character in any statement throws a SyntaxError exception. To prevent the exception, ensure no invalid token are mistakenly included (e.g: leading zero)." },
            { "KeyError", "Trying to retrieve an element from a collection using a key that does not exist in that collection throws a KeyError exception. To prevent the exception, ensure that the key exist in the collection and/or that collection is not empty."},
            { "NoMethodMatchError", "Trying to give parameters of the wrong type throws a TypeError exception. To prevent the exception, ensure each parameter type matches those required by this method:" },
            { "UnsupportedOperandError", "Trying to perform a summation, subtraction, multiplication or division between a decimal.Decimal and a float throws a TypeError exception. To prevent the exception, ensure that both values share the same type, either decimal.Decimal or float."},
            { "ZeroDivisionError", "Trying to divide an integer or Decimal number by zero throws a ZeroDivisionError exception. To prevent the exception, ensure that the denominator in a division operation with integer or Decimal values is non-zero." },
        };

        /// <summary>
        /// Parses an <see cref="Exception"/> object into an new <see cref="Exception"/> with a legible message.
        /// </summary>
        /// <param name="exception"><see cref="Exception"/> object to parse.</param>
        /// <returns>Parsed exception</returns>
        public Exception Parse(Exception exception)
        {
            var original = exception;
            if (exception.InnerException != null)
            {
                exception = Parse(exception.InnerException);
            }

            var message = CreateLegibleMessage(exception.Message);
            var errorLine = CreateErrorLine(exception.StackTrace);
            message = $"{message}{errorLine}";

            if (original.GetType() == typeof(InitializeException))
            {
                message = $"In the Initialize method, {message.Substring(0, 1).ToLower()}{message.Substring(1)}";
                return new InitializeException(message, original);
            }
            else if (original.GetType() == typeof(ScheduledEventException))
            {
                message = $"In one of your Schedule Events, {message.Substring(0, 1).ToLower()}{message.Substring(1)} ";
                return new ScheduledEventException(message, original);
            }

            return new Exception(message, original);
        }

        private string CreateLegibleMessage(string value)
        {
            var colon = value.IndexOf(':');
            if (colon < 0)
            {
                return value;
            }

            var type = value.Substring(0, colon).Trim();
            var input = value.Substring(1 + colon).Trim();
            var message = value;

            switch (type)
            {
                case "KeyError":
                    var key = GetStringBetweenChar(input, '[', ']');
                    if (key == null)
                    {
                        key = GetStringBetweenChar(input, '\'', '\'');
                    }
                    message = $"{_commonErrors[type]} Key: {key}.";
                    break;
                case "SyntaxError":
                    if (input.Contains("invalid token"))
                    {
                        message = _commonErrors["InvalidTokenError"];
                        var errorLine = GetStringBetweenChar(input, '(', ')');
                        var parts = errorLine.Split(' ');
                        var line = int.Parse(parts[2]) + _offset;
                        errorLine = errorLine.Replace(parts[2], line.ToString());
                        message = $"{message}{Environment.NewLine}  in {errorLine}{Environment.NewLine}";
                    }
                    break;
                case "TypeError":
                    if (input.Contains("unsupported operand"))
                    {
                        message = _commonErrors["UnsupportedOperandError"];
                    }
                    if (input.Contains("No method matches"))
                    {
                        var startIndex = input.LastIndexOf(" ");
                        message = _commonErrors["NoMethodMatchError"] + input.Substring(startIndex);
                    }
                    break;
                case "ZeroDivisionError":
                    message = _commonErrors[type];
                    break;
                default:
                    break;
            }

            return message;
        }

        private string CreateErrorLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Get the place where the error occurred in the PythonException.StackTrace
            var stack = value.Replace("\\\\", "/").Split(new[] { @"\n" }, StringSplitOptions.RemoveEmptyEntries);
            var baseScript = stack[0].Substring(1 + stack[0].IndexOf('\"')).Split('\"')[0];
            var directory = baseScript.Substring(0, baseScript.LastIndexOf('/'));
            baseScript = baseScript.Substring(1 + directory.Length);

            var errorLine = string.Empty;

            for (var i = 0; i < stack.Length; i += 2)
            {
                if (stack[i].Contains(directory))
                {
                    var index = stack[i].IndexOf(directory) + directory.Length + 1;
                    var info = stack[i].Substring(index).Split(',');

                    var script = info[0].Remove(info[0].Length - 1);
                    var line = int.Parse(info[1].Remove(0, 6));
                    var method = info[2].Replace("in", "at");
                    var statement = stack[i + 1].Trim();

                    // Adds offset to account headers
                    // Yields wrong error line if running Lean locally 
                    line += script == baseScript ? _offset : 0;

                    errorLine = $"{Environment.NewLine}  {method} in {script}:line {line} :: {statement}{Environment.NewLine}";
                }
            }

            return errorLine;
        }

        private string GetStringBetweenChar(string value, char left, char right)
        {
            var startIndex = 1 + value.IndexOf(left);
            var length = value.IndexOf(right, startIndex) - startIndex;
            if (length > 0)
            {
                return value.Substring(startIndex, length);
            }
            return null;
        }
    }
}