/*
Functions for the AWS Lambda demo. (Hasn't been tested in years.)
See Copyright Notice in LICENSE.TXT
*/

#if PEBBLE_LAMBDA

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ArgList = System.Collections.Generic.List<Pebble.ITypeDef>;

namespace Pebble {

    public class LambdaLib {

        public static List<string> printCache = new List<string>();

        public static void Register(ScriptEngine engine) {

            {
                // string Print(...)
                FunctionValue_Host.EvaluateDelegate eval = (context, args, thisScope) => {
                    string ret = "";
                    for (int ii = 0; ii < args.Count; ++ii) {
                        object val = args[ii];
                        ret += val.ToString();
                    }

                    printCache.Add(ret);

                    return ret;
                };

                FunctionValue newValue = new FunctionValue_Host(IntrinsicTypeDefs.STRING, new List<ITypeDef> { IntrinsicTypeDefs.ANY }, eval, true);
                engine.AddBulitInFunction(newValue, "Print");
            }
        }
    }
}

#endif