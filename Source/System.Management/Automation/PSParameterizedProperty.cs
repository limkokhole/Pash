﻿// Copyright (C) Pash Contributors. License: GPL/BSD. See https://github.com/Pash-Project/Pash/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Extensions.Types;

namespace System.Management.Automation
{
    public class PSParameterizedProperty : PSMethodInfo
    {
        private Type _classType;
        private object _instance;
        private PropertyInfo _propertyInfo;
        private Collection<string> _overloadDefinitions;
        private bool _invokeSetter;

        public bool IsGettable { get; private set; }
        public bool IsSettable { get; private set; }

        internal PSParameterizedProperty(PropertyInfo propertyInfo, Type classType, object owner, bool isInstance)
             : base()
        {
            _classType = classType;
            _instance = owner;
            _propertyInfo = propertyInfo;

            IsInstance = isInstance;
            Name = propertyInfo.Name;
            IsGettable = propertyInfo.CanRead;
            IsSettable = propertyInfo.CanWrite;
        }

        public override PSMemberTypes MemberType
        {
            get
            {
                return PSMemberTypes.ParameterizedProperty;
            }
        }

        public override string TypeNameOfValue
        {
            get
            {
                return _propertyInfo.PropertyType.FullName;
            }
        }

        public override Collection<string> OverloadDefinitions
        {
            get
            {
                if (_overloadDefinitions == null)
                {
                    _overloadDefinitions = new Collection<string>(GetDefinitions().ToList());
                }
                return _overloadDefinitions;
            }
        }

        private MethodInfo[] _overloads;

        protected override MethodInfo[] Overloads
        {
            get
            {
                if (_overloads == null)
                {
                    _overloads = (from propertyInfo in GetProperties()
                                  select GetMethod(propertyInfo, _invokeSetter)).ToArray();
                }
                return _overloads;
            }
        }

        public override object Invoke(params object[] arguments)
        {
            try
            {
                _invokeSetter = false;
                return InvokeMethod(_instance, arguments);
            }
            catch (MethodException ex)
            {
                throw new GetValueInvocationException(string.Format(CultureInfo.CurrentCulture,
                    "Exception getting \"{0}\": \"{1}\"", Name, ex.Message), ex);
            }
        }

        public void InvokeSet(object valueToSet, params object[] arguments)
        {
            try
            {
                _invokeSetter = true;
                var modifiedArguments = new List<object>(arguments);
                modifiedArguments.Add(valueToSet);
                InvokeMethod(_instance, modifiedArguments.ToArray());
            }
            catch (MethodException ex)
            {
                throw new SetValueInvocationException(string.Format(CultureInfo.CurrentCulture,
                    "Exception setting \"{0}\": \"{1}\"", Name, ex.Message), ex);
            }
        }

        public override PSMemberInfo Copy()
        {
            return new PSParameterizedProperty(_propertyInfo, _classType, _instance, IsInstance);
        }

        internal static bool IsParameterizedProperty(PropertyInfo propertyInfo)
        {
            if (propertyInfo.CanRead)
            {
                MethodInfo getMethodInfo = propertyInfo.GetGetMethod();
                if (getMethodInfo.GetParameters().Any())
                {
                    return true;
                }
            }

            if (propertyInfo.CanWrite)
            {
                MethodInfo setMethodInfo = propertyInfo.GetSetMethod();
                if (setMethodInfo != null && setMethodInfo.GetParameters().Count() > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<PropertyInfo> GetProperties()
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.FlattenHierarchy;
            flags |= IsInstance ? BindingFlags.Instance : BindingFlags.Static;

            return from propertyInfo in _classType.GetProperties(flags)
                   where propertyInfo.Name == Name
                   select propertyInfo;
        }

        protected override MethodInfo GetMethod(Type[] argTypes)
        {
            return GetMethod(_propertyInfo, _invokeSetter);
        }

        static private MethodInfo GetMethod(PropertyInfo propertyInfo, bool useSetter)
        {
            if (useSetter)
            {
                return propertyInfo.GetSetMethod();
            }

            return propertyInfo.GetGetMethod();
        }

        private IEnumerable<string> GetDefinitions()
        {
            return from propertyInfo in GetProperties()
                   select GetDefinition(propertyInfo);
        }

        private string GetDefinition(PropertyInfo propertyInfo)
        {
            MethodInfo getMethod = propertyInfo.GetGetMethod();

            var definition = new StringBuilder();
            if (propertyInfo.CanRead)
            {
                definition.Append(getMethod.ReturnType.FriendlyName());
            }
            else
            {
                definition.Append("void");
            }
            definition.Append(' ');

            definition.Append(Name);

            ParameterInfo[] parameters = null;
            if (propertyInfo.CanRead)
            {
                parameters = getMethod.GetParameters();
            }
            else
            {
                parameters = propertyInfo.GetSetMethod().GetParameters();
                parameters = parameters.Take(parameters.Length - 1).ToArray();
            }

            definition.Append('(');
            definition.Append(string.Join(", ", parameters.Select(parameter => GetParameterDefinition(parameter))));
            definition.Append(") ");

            definition.Append('{');
            if (_propertyInfo.CanRead)
            {
                definition.Append("get;");
            }

            if (_propertyInfo.CanWrite && _propertyInfo.GetSetMethod() != null)
            {
                definition.Append("set;");
            }
            definition.Append('}');

            return definition.ToString();
        }

        private static string GetParameterDefinition(ParameterInfo parameter)
        {
            return parameter.ParameterType.FriendlyName() + " " + parameter.Name;
        }

        protected override int GetArgumentsLength(object[] arguments)
        {
            if (_invokeSetter)
            {
                return arguments.Length - 1;
            }
            return arguments.Length;
        }
    }
}
