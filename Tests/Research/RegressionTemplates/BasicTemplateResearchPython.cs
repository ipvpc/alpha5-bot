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

using QuantConnect.Interfaces;

namespace QuantConnect.Tests.Research.RegressionTemplates
{
    public class BasicTemplateResearchPython : IRegressionResearchDefinition
    {
        /// <remarks>Requires to be implemented last in the file <see cref="ResearchRegressionTests.UpdateResearchRegressionOutputInSourceFile"/>
        /// get should start from next line</remarks>
        public string ExpectedOutput =>
            "{\r\n \"cells\": [\r\n  {\r\n   \"cell_type\": \"markdown\",\r\n   \"id\": \"ce192822\",\r\n   \"metadata\": {\r\n    \"papermill\": {\r\n     \"duration\": 0.00401,\r\n     \"end_time\": \"2022-03-01T19:21:49.742838\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:49.738828\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"source\": [\r\n    \"![QuantConnect Logo](https://cdn.quantconnect.com/web/i/qc_notebook_logo_rev0.png)\\n\",\r\n    \"## Welcome to The QuantConnect Research Page\\n\",\r\n    \"#### Refer to this page for documentation https://www.quantconnect.com/docs/research/overview#\\n\",\r\n    \"#### Contribute to this template file https://github.com/QuantConnect/Lean/blob/master/Research/BasicQuantBookTemplate.ipynb\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"markdown\",\r\n   \"id\": \"eacaf2e5\",\r\n   \"metadata\": {\r\n    \"papermill\": {\r\n     \"duration\": 0.002991,\r\n     \"end_time\": \"2022-03-01T19:21:49.749827\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:49.746836\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"source\": [\r\n    \"## QuantBook Basics\\n\",\r\n    \"\\n\",\r\n    \"### Start QuantBook\\n\",\r\n    \"- Add the references and imports\\n\",\r\n    \"- Create a QuantBook instance\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": 1,\r\n   \"id\": \"d7fa2ad1\",\r\n   \"metadata\": {\r\n    \"execution\": {\r\n     \"iopub.execute_input\": \"2022-03-01T19:21:49.759827Z\",\r\n     \"iopub.status.busy\": \"2022-03-01T19:21:49.759827Z\",\r\n     \"iopub.status.idle\": \"2022-03-01T19:21:49.771950Z\",\r\n     \"shell.execute_reply\": \"2022-03-01T19:21:49.771950Z\"\r\n    },\r\n    \"papermill\": {\r\n     \"duration\": 0.019126,\r\n     \"end_time\": \"2022-03-01T19:21:49.771950\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:49.752824\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": [\r\n    \"import warnings\\n\",\r\n    \"warnings.filterwarnings(\\\"ignore\\\")\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": 2,\r\n   \"id\": \"a59fac37\",\r\n   \"metadata\": {\r\n    \"execution\": {\r\n     \"iopub.execute_input\": \"2022-03-01T19:21:49.780950Z\",\r\n     \"iopub.status.busy\": \"2022-03-01T19:21:49.780950Z\",\r\n     \"iopub.status.idle\": \"2022-03-01T19:21:50.977694Z\",\r\n     \"shell.execute_reply\": \"2022-03-01T19:21:50.977694Z\"\r\n    },\r\n    \"papermill\": {\r\n     \"duration\": 1.201737,\r\n     \"end_time\": \"2022-03-01T19:21:50.977694\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:49.775957\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": [\r\n    \"# Load in our startup script, required to set runtime for PythonNet\\n\",\r\n    \"%run ./start.py\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": 3,\r\n   \"id\": \"55f1d01f\",\r\n   \"metadata\": {\r\n    \"execution\": {\r\n     \"iopub.execute_input\": \"2022-03-01T19:21:51.263694Z\",\r\n     \"iopub.status.busy\": \"2022-03-01T19:21:51.263694Z\",\r\n     \"iopub.status.idle\": \"2022-03-01T19:21:51.309294Z\",\r\n     \"shell.execute_reply\": \"2022-03-01T19:21:51.310302Z\"\r\n    },\r\n    \"papermill\": {\r\n     \"duration\": 0.328607,\r\n     \"end_time\": \"2022-03-01T19:21:51.310302\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:50.981695\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": [\r\n    \"# Create an instance\\n\",\r\n    \"qb = QuantBook()\\n\",\r\n    \"\\n\",\r\n    \"# Select asset data\\n\",\r\n    \"spy = qb.AddEquity(\\\"SPY\\\")\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"markdown\",\r\n   \"id\": \"826fe2d0\",\r\n   \"metadata\": {\r\n    \"papermill\": {\r\n     \"duration\": 0.004,\r\n     \"end_time\": \"2022-03-01T19:21:51.317301\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:51.313301\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"source\": [\r\n    \"### Historical Data Requests\\n\",\r\n    \"\\n\",\r\n    \"We can use the QuantConnect API to make Historical Data Requests. The data will be presented as multi-index pandas.DataFrame where the first index is the Symbol.\\n\",\r\n    \"\\n\",\r\n    \"For more information, please follow the [link](https://www.quantconnect.com/docs#Historical-Data-Historical-Data-Requests).\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": 4,\r\n   \"id\": \"d7e39d99\",\r\n   \"metadata\": {\r\n    \"execution\": {\r\n     \"iopub.execute_input\": \"2022-03-01T19:21:51.326736Z\",\r\n     \"iopub.status.busy\": \"2022-03-01T19:21:51.326736Z\",\r\n     \"iopub.status.idle\": \"2022-03-01T19:21:51.341733Z\",\r\n     \"shell.execute_reply\": \"2022-03-01T19:21:51.340730Z\"\r\n    },\r\n    \"papermill\": {\r\n     \"duration\": 0.021432,\r\n     \"end_time\": \"2022-03-01T19:21:51.341733\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:51.320301\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": [\r\n    \"startDate = DateTime(2021,1,1)\\n\",\r\n    \"endDate = DateTime(2021,12,31)\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": 5,\r\n   \"id\": \"3bcebce3\",\r\n   \"metadata\": {\r\n    \"execution\": {\r\n     \"iopub.execute_input\": \"2022-03-01T19:21:51.353350Z\",\r\n     \"iopub.status.busy\": \"2022-03-01T19:21:51.353350Z\",\r\n     \"iopub.status.idle\": \"2022-03-01T19:21:51.497639Z\",\r\n     \"shell.execute_reply\": \"2022-03-01T19:21:51.498647Z\"\r\n    },\r\n    \"papermill\": {\r\n     \"duration\": 0.153295,\r\n     \"end_time\": \"2022-03-01T19:21:51.498647\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:51.345352\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"scrolled\": true,\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": [\r\n    \"# Gets historical data from the subscribed assets, the last 360 datapoints with daily resolution\\n\",\r\n    \"h1 = qb.History(qb.Securities.Keys, startDate, endDate, Resolution.Daily)\\n\",\r\n    \"\\n\",\r\n    \"if h1.shape[0] < 1:\\n\",\r\n    \"    raise Exception(\\\"History request resulted in no data\\\")\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"markdown\",\r\n   \"id\": \"865ef98a\",\r\n   \"metadata\": {\r\n    \"papermill\": {\r\n     \"duration\": 0.003893,\r\n     \"end_time\": \"2022-03-01T19:21:51.506535\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:51.502642\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"source\": [\r\n    \"### Indicators\\n\",\r\n    \"\\n\",\r\n    \"We can easily get the indicator of a given symbol with QuantBook. \\n\",\r\n    \"\\n\",\r\n    \"For all indicators, please checkout QuantConnect Indicators [Reference Table](https://www.quantconnect.com/docs#Indicators-Reference-Table)\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": 6,\r\n   \"id\": \"cf2463c9\",\r\n   \"metadata\": {\r\n    \"execution\": {\r\n     \"iopub.execute_input\": \"2022-03-01T19:21:51.521583Z\",\r\n     \"iopub.status.busy\": \"2022-03-01T19:21:51.521583Z\",\r\n     \"iopub.status.idle\": \"2022-03-01T19:21:51.592890Z\",\r\n     \"shell.execute_reply\": \"2022-03-01T19:21:51.592890Z\"\r\n    },\r\n    \"papermill\": {\r\n     \"duration\": 0.083306,\r\n     \"end_time\": \"2022-03-01T19:21:51.592890\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:51.509584\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": [\r\n    \"# Example with BB, it is a datapoint indicator\\n\",\r\n    \"# Define the indicator\\n\",\r\n    \"bb = BollingerBands(30, 2)\\n\",\r\n    \"\\n\",\r\n    \"# Gets historical data of indicator\\n\",\r\n    \"bbdf = qb.Indicator(bb, \\\"SPY\\\", startDate, endDate, Resolution.Daily)\\n\",\r\n    \"\\n\",\r\n    \"# drop undesired fields\\n\",\r\n    \"bbdf = bbdf.drop('standarddeviation', 1)\\n\",\r\n    \"\\n\",\r\n    \"if bbdf.shape[0] < 1:\\n\",\r\n    \"    raise Exception(\\\"Bollinger Bands resulted in no data\\\")\"\r\n   ]\r\n  },\r\n  {\r\n   \"cell_type\": \"code\",\r\n   \"execution_count\": null,\r\n   \"id\": \"0589c1f1\",\r\n   \"metadata\": {\r\n    \"papermill\": {\r\n     \"duration\": 0.004003,\r\n     \"end_time\": \"2022-03-01T19:21:51.600893\",\r\n     \"exception\": false,\r\n     \"start_time\": \"2022-03-01T19:21:51.596890\",\r\n     \"status\": \"completed\"\r\n    },\r\n    \"tags\": []\r\n   },\r\n   \"outputs\": [],\r\n   \"source\": []\r\n  }\r\n ],\r\n \"metadata\": {\r\n  \"kernelspec\": {\r\n   \"display_name\": \"Python 3 (ipykernel)\",\r\n   \"language\": \"python\",\r\n   \"name\": \"python3\"\r\n  },\r\n  \"language_info\": {\r\n   \"codemirror_mode\": {\r\n    \"name\": \"ipython\",\r\n    \"version\": 3\r\n   },\r\n   \"file_extension\": \".py\",\r\n   \"mimetype\": \"text/x-python\",\r\n   \"name\": \"python\",\r\n   \"nbconvert_exporter\": \"python\",\r\n   \"pygments_lexer\": \"ipython3\",\r\n   \"version\": \"3.6.8\"\r\n  },\r\n  \"papermill\": {\r\n   \"default_parameters\": {},\r\n   \"duration\": 3.280999,\r\n   \"end_time\": \"2022-03-01T19:21:52.060560\",\r\n   \"environment_variables\": {},\r\n   \"exception\": null,\r\n   \"input_path\": \"D:\\\\quantconnect\\\\Lean\\\\Tests\\\\bin\\\\Debug\\\\BasicTemplateResearchPython.ipynb\",\r\n   \"output_path\": \"D:\\\\quantconnect\\\\Lean\\\\Tests\\\\bin\\\\Debug\\\\BasicTemplateResearchPython-output.ipynb\",\r\n   \"parameters\": {},\r\n   \"start_time\": \"2022-03-01T19:21:48.779561\",\r\n   \"version\": \"2.3.4\"\r\n  }\r\n },\r\n \"nbformat\": 4,\r\n \"nbformat_minor\": 5\r\n}";
    }
}
