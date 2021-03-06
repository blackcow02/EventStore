﻿using System;
using System.Linq;
using EventStore.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    internal class OptionFlagContainer : IOptionContainer
    {
        object IOptionContainer.FinalValue { get { return FinalValue; } }

        public bool FinalValue
        {
            get
            {
                if (Value == null && _default == null)
                    throw new InvalidOperationException(string.Format("No value provided for option '{0}'.", Name));
                return (Value ?? _default).Value;
            }
        }

        public string Name { get; private set; }
        public bool? Value { get; set; }
        public bool IsSet { get { return Value.HasValue; } }
        public bool HasDefault { get { return _default.HasValue; } }

        public OptionOrigin Origin { get; set; }
        public string OriginName { get; set; }
        public string OriginOptionName { get; set; }

        private readonly string _cmdPrototype;
        private readonly string _envVariable;
        private readonly string[] _jsonPath;
        private readonly bool? _default;
        private bool _stopIfSet;

        public OptionFlagContainer(string name, string cmdPrototype, string envVariable, string[] jsonPath, bool? @default, bool stopIfSet = false)
        {
            _stopIfSet = stopIfSet;
            Ensure.NotNullOrEmpty(name, "name");
            if (jsonPath != null && jsonPath.Length == 0)
                throw new ArgumentException("JsonPath array is empty.", "jsonPath");

            Name = name;
            _cmdPrototype = cmdPrototype;
            _envVariable = envVariable;
            _jsonPath = jsonPath;
            _default = @default;

            Origin = OptionOrigin.None;
            OriginName = "<uninitialized>";
            OriginOptionName = name;
        }

        public void ParsingFromCmdLine(string flagArgName)
        {
            Origin = OptionOrigin.CommandLine;
            OriginName = OptionOrigin.CommandLine.ToString();
            OriginOptionName = flagArgName ?? _cmdPrototype.Split('|').Last().Trim('=');

            if (Value.HasValue)
                throw new OptionException(string.Format("Option {0} is set more than once.", OriginOptionName), OriginOptionName);

            Value = flagArgName != null;
        }

        public bool DontParseFurther
        {
            get { return _stopIfSet && IsSet; }
        }

        public void ParseFromEnvironment()
        {
            if (_envVariable.IsEmptyString())
                return;

            var varValue = Environment.GetEnvironmentVariable(_envVariable);
            if (varValue == null)
                return;

            Origin = OptionOrigin.Environment;
            OriginName = OptionOrigin.Environment.ToString();
            OriginOptionName = _envVariable;

            switch (varValue)
            {
                case "0":
                    Value = false;
                    break;
                case "1":
                    Value = true;
                    break;
                default:
                    throw new OptionException(
                            string.Format(
                                          "Invalid value for flag in environment variable {0} (value: '{1}'), valid values are '0' and '1'.",
                                          _envVariable,
                                          varValue),
                            _envVariable);
            }
        }

        public void ParseFromConfig(JObject json, string configName)
        {
            Ensure.NotNullOrEmpty(configName, "configName");
            if (_jsonPath == null)
                return;

            Origin = OptionOrigin.Config;
            OriginName = configName;
            OriginOptionName = string.Join(".", _jsonPath);

            var value = OptionContainerHelpers.GetTokenByJsonPath(json, _jsonPath);
            if (value == null)
                return;

            if (value.Type != JTokenType.Boolean)
            {
                throw new OptionException(
                        string.Format("Property '{0}' (value: {1}) in JSON config at '{2}' is not boolean value.",
                                      OriginOptionName,
                                      value,
                                      configName),
                        OriginOptionName);
            }

            Value = value.Value<bool>();
        }
    }
}