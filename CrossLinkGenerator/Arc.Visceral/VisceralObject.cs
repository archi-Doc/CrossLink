﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1602 // Enumeration items should be documented

namespace Arc.Visceral
{
    public enum VisceralObjectKind
    {
        None,
        Class, // Type (ITypeSymbol)
        Interface, // Type (ITypeSymbol)
        Struct, // Type (ITypeSymbol)
        Record, // Type (ITypeSymbol)
        Enum, // Type (INamedTypeSymbol)
        TypeParameter, // TypeParameter (ITypeSymbol) "T"
        Field, // Value (IFieldSymbol)
        Property, // Value (IPropertySymbol)
        Method, // Method (IMethodSymbol)
    }

    [Flags]
    public enum VisceralTarget
    {
        None = 0,
        Field = 1,
        Property = 2,
        FieldProperty = 3,
        Method = 4,
        TypeParameter = 8,
        Class = 16,
        Struct = 32,
        Interface = 64,
        Record = 128,
        Type = Class | Struct | Interface | Record,
        All = ~0,
    }

    public class VisceralAttribute : IComparable<VisceralAttribute>
    {
        public VisceralAttribute(string fullName, AttributeData attributeData)
        {// Name, Arguments = KeyValuePair<string, object?>
            // Constructor Argument: Name = ""
            this.FullName = fullName;

            // var builder = ImmutableArray.CreateBuilder<KeyValuePair<string, object?>>();
            // builder.Add(new KeyValuePair<string, object?>(string.Empty, x.Value));
            // builder.Add(new KeyValuePair<string, object?>(x.Key, x.Value.Value));
            // this.Arguments = builder.ToImmutable();

            var n = 0;
            this.ConstructorArguments = new object?[attributeData.ConstructorArguments.Length];
            foreach (var x in attributeData.ConstructorArguments)
            {
                this.ConstructorArguments[n++] = x.Value;
            }

            n = 0;
            this.NamedArguments = new KeyValuePair<string, object?>[attributeData.NamedArguments.Length];
            foreach (var x in attributeData.NamedArguments)
            {
                this.NamedArguments[n++] = new KeyValuePair<string, object?>(x.Key, x.Value.Value);
            }

            this.attributeData = attributeData;
        }

        public VisceralAttribute(string fullName, CustomAttributeData attributeData)
        {// Name, Arguments = KeyValuePair<string, object?>
            // Constructor Argument: Name = ""
            this.FullName = fullName;

            var n = 0;
            this.ConstructorArguments = new object?[attributeData.ConstructorArguments.Count];
            foreach (var x in attributeData.ConstructorArguments)
            {
                this.ConstructorArguments[n++] = x.Value;
            }

            n = 0;
            this.NamedArguments = new KeyValuePair<string, object?>[attributeData.NamedArguments.Count];
            foreach (var x in attributeData.NamedArguments)
            {
                this.NamedArguments[n++] = new KeyValuePair<string, object?>(x.MemberName, x.TypedValue.Value);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is VisceralAttribute x)
            {
                if (this.FullName != x.FullName)
                {
                    return false;
                }
                else if (!this.ConstructorArguments.SequenceEqual(x.ConstructorArguments))
                {
                    return false;
                }
                else if (!this.NamedArguments.SequenceEqual(x.NamedArguments))
                {
                    return false;
                }

                return true; // Identical
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public int CompareTo(VisceralAttribute other)
        {
            return string.Compare(this.FullName, other.FullName);
        }

        /// <summary>
        /// Gets the fully qualified name of the attribute.
        /// </summary>
        public string FullName { get; }

        // public ImmutableArray<KeyValuePair<string, object?>> Arguments { get; }

        /// <summary>
        /// Gets the constructor attribute arguments (object? value).
        /// </summary>
        public object?[] ConstructorArguments { get; }

        /// <summary>
        /// Gets the named attribute arguments (string name, object? value).
        /// </summary>
        public KeyValuePair<string, object?>[] NamedArguments { get; }

        public Location Location
        {
            get
            {
                if (this.attributeData != null)
                {
                    var first = this.attributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                    if (first != null)
                    {
                        return first;
                    }
                }

                return Location.None;
            }
        }

        protected AttributeData? attributeData;
    }

    public enum NullableAnnotation : byte
    {
        /// <summary>
        /// The expression has not been analyzed, or the syntax is not an expression (such as a statement).
        /// </summary>
        None = 0,

        /// <summary>
        /// The expression is not annotated (does not have a ?).
        /// </summary>
        NotAnnotated = 1,

        /// <summary>
        /// The expression is annotated (does have a ?).
        /// </summary>
        Annotated = 2,
    }

    public class WithNullable<T>
        where T : VisceralObjectBase<T>, new()
    {// Not a smart way, but I think this is the best way to handle nullable type.
        public T Object { get; }

        public NullableAnnotation Nullable { get; }

        public WithNullable(T obj, ISymbol symbol, NullableAnnotation n)
        {
            this.Object = obj;
            this.Nullable = n;
            this.symbol = symbol;
        }

        public WithNullable(T obj, ISymbol symbol, Microsoft.CodeAnalysis.NullableAnnotation n)
        {
            this.Object = obj;
            this.Nullable = (NullableAnnotation)n;
            this.symbol = symbol;
        }

        public WithNullable<T>? Array_ElementWithNullable
        {
            get
            {
                var t = this.Object.Array_Element;
                if (t == null)
                {
                    return null;
                }

                if (this.symbol is IArrayTypeSymbol ats)
                {// Array type symbol
                    return new WithNullable<T>(t, ats.ElementType, ats.ElementNullableAnnotation);
                }
                else
                {
                    return null;
                }
            }
        }

        private WithNullable<T>[]? genericsArgumentsWithNullable;

        public WithNullable<T>[] Generics_ArgumentsWithNullable
        {
            get
            {
                if (this.genericsArgumentsWithNullable == null)
                {
                    if (this.symbol is INamedTypeSymbol ts)
                    {
                        this.genericsArgumentsWithNullable = new WithNullable<T>[ts.TypeArguments.Length];
                        var n = 0;
                        foreach (var x in ts.TypeArguments)
                        {
                            var t = this.Object.Body.Add(x);
                            if (t != null)
                            {
                                this.genericsArgumentsWithNullable[n++] = new WithNullable<T>(t, x, x.NullableAnnotation);
                            }
                        }

                        if (n != ts.TypeArguments.Length)
                        {
                            Array.Resize(ref this.genericsArgumentsWithNullable, n);
                        }
                    }
                    else
                    {
                        this.genericsArgumentsWithNullable = Array.Empty<WithNullable<T>>();
                    }
                }

                return this.genericsArgumentsWithNullable;
            }

            protected set
            {
                this.genericsArgumentsWithNullable = value;
            }
        }

        public override int GetHashCode()
        {// Consider HashCode.Combine();
            unchecked
            {
                return this.FullNameWithNullable.GetHashCode();
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != typeof(WithNullable<T>))
            {
                return false;
            }

            var target = (WithNullable<T>)obj;
            if (this.FullNameWithNullable != target.FullNameWithNullable)
            {
                return false;
            }

            // Identical
            return true;
        }

        public override string ToString() => this.FullNameWithNullable;

        private ISymbol symbol;

        public string FullName => this.Object.FullName;

        private string? fullNameWithNullable;

        public string FullNameWithNullable
        {// Fully qualified name of the type. Namespace.Class+LocalName, + Nullable annotation
            // Namespace.class.name, System.Int32, class.Method(System.String)
            get
            {
                if (this.fullNameWithNullable == null)
                {
                    this.fullNameWithNullable = this.Object.Body.SymbolToFullName(this.symbol, true);

                    /*if (this.Nullable == NullableAnnotation.Annotated)
                    {// T?
                        if (this.fullNameWithNullable.Last() != '?')
                        {
                            this.fullNameWithNullable += '?';
                        }
                    }
                    else
                    {// T
                        if (this.fullNameWithNullable.Last() == '?')
                        {
                            this.fullNameWithNullable = this.fullNameWithNullable.Remove(this.fullNameWithNullable.Length - 1);
                        }
                    }*/
                }

                return this.fullNameWithNullable;
            }

            protected set
            {
                this.fullNameWithNullable = value;
            }
        }
    }

    public abstract class VisceralObjectBase<T> : IComparable<T>
        where T : VisceralObjectBase<T>, new()
    { // Converts ISymbol/Type to VisceralObject.
        public VisceralObjectBase()
        {
        }

        public override string ToString() => this.FullName;

        public override int GetHashCode() => this.FullName.GetHashCode();

        public override bool Equals(object obj)
        {// FullName is considered to be a unique identifier.
            if (obj is VisceralObjectBase<T> x)
            {
                return this.FullName == x.FullName;
            }

            return false;
        }

        public int CompareTo(T other)
        {
            return string.Compare(this.FullName, other.FullName);
        }

        public bool DeepEquals(T? target) => this.DeepEquals(target, new Stack<T>());

        public bool DeepEquals(T? target, Stack<T> circularDependency)
        {
            var self = (T)this;
            if (circularDependency.Contains(self))
            {
                return true;
            }

            circularDependency.Push(self);
            try
            {
                return this.DeepEqualsCore(target, circularDependency);
            }
            finally
            {
                circularDependency.Pop();
            }
        }

        public virtual bool DeepEqualsCore(T? target, Stack<T> circularDependency)
        {// Omit: Type, IsPartial, some properties (Objects generated from typeof and ISymbol are not exactly the same)
            if (target == null)
            {
                return false;
            }
            else if (this.AccessibilityName != target.AccessibilityName)
            {
                return false;
            }
            else if (this.Array_Rank != target.Array_Rank)
            {
                return false;
            }
            else if (!this.AllAttributes.SequenceEqual(target.AllAttributes))
            {
                return false;
            }
            else if (!VisceralHelper.SortAndSequenceEqual(this.AllInterfaces, target.AllInterfaces))
            {
                return false;
            }
            else if (!(this.Enum_UnderlyingTypeObject == null && target.Enum_UnderlyingTypeObject == null) && this.Enum_UnderlyingTypeObject?.DeepEquals(target.Enum_UnderlyingTypeObject, circularDependency) != true)
            {
                return false;
            }
            else if (this.FullName != target.FullName)
            {
                return false;
            }
            else if (this.Generics_Kind != target.Generics_Kind)
            {
                return false;
            }
            else if (!(this.BaseObject == null && target.BaseObject == null) && this.BaseObject?.DeepEquals(target.BaseObject, circularDependency) != true)
            {
                return false;
            }
            else if (this.Kind.IsValue() &&
                !(this.TypeObject == null && target.TypeObject == null) && this.TypeObject?.DeepEquals(target.TypeObject, circularDependency) != true)
            {
                return false;
            }
            else if (this.IsConstructedFromNullable != target.IsConstructedFromNullable)
            {
                return false;
            }
            else if (this.IsPrimitive != target.IsPrimitive)
            {
                return false;
            }
            else if (this.IsPublic != target.IsPublic)
            {
                return false;
            }
            else if (this.IsReadable != target.IsReadable)
            {
                return false;
            }
            else if (this.IsReadOnly != target.IsReadOnly)
            {
                return false;
            }
            else if (this.IsSerializable != target.IsSerializable)
            {
                return false;
            }
            else if (this.IsSystem != target.IsSystem)
            {
                return false;
            }
            else if (this.IsTuple != target.IsTuple)
            {
                return false;
            }
            else if (this.IsWritable != target.IsWritable)
            {
                return false;
            }
            else if (this.Kind != target.Kind)
            {
                return false;
            }
            else if (this.LocalName != target.LocalName)
            {
                return false;
            }
            else if (this.Method_IsConstructor != target.Method_IsConstructor)
            {
                return false;
            }
            else if (!VisceralHelper.SortAndSequenceEqual(this.Method_Parameters, target.Method_Parameters))
            {
                return false;
            }
            else if (this.SimpleName != target.SimpleName)
            {
                return false;
            }

            var members = this.AllMembers.Sort();
            var members2 = target.AllMembers.Sort();
            if (members.Length != members2.Length)
            {
                return false;
            }

            for (var i = 0; i < members.Length; i++)
            {
                if (!members[i].DeepEquals(members2[i], circularDependency))
                {
                    return false;
                }
            }

            var genericsArguments = this.Generics_Arguments.Sort();
            var genericsArguments2 = target.Generics_Arguments.Sort();
            if (genericsArguments.Length != genericsArguments2.Length)
            {
                return false;
            }

            for (var i = 0; i < genericsArguments.Length; i++)
            {
                if (!genericsArguments[i].DeepEquals(genericsArguments2[i], circularDependency))
                {
                    return false;
                }
            }

            return true; // Identical
        }

        public bool Initialize(VisceralBody<T> body, ISymbol symbol, string fullName)
        {
            this.Body = body;
            this.symbol = symbol;
            this.FullName = fullName;
            this.Kind = this.ISymbolToObjectKind(this.symbol);
            if (this.Kind == VisceralObjectKind.None)
            {
                return false;
            }

            var primitiveType = VisceralHelper.Primitives_ShortenName(fullName);
            if (primitiveType != null)
            {// Primitive type
                this.IsPrimitive = true;
                this.BaseObject = null;
                this.AllMembers = ImmutableArray<T>.Empty;
                this.AllAttributes = ImmutableArray<VisceralAttribute>.Empty;
                return true;
            }

            if (this.symbol is ITypeSymbol typeSymbol)
            { // Type (class, interface, struct, record)
            }
            else if (this.Kind == VisceralObjectKind.Method)
            {// Method
                var ms = (IMethodSymbol)this.symbol;
                if (ms.AssociatedSymbol != null)
                {// Setter or Getter method.
                    this.Kind = VisceralObjectKind.None;
                    return false;
                }
            }
            else if (this.Kind == VisceralObjectKind.Field)
            {// Field
                var fs = (IFieldSymbol)this.symbol;
                if (fs.AssociatedSymbol != null)
                {// Backing field.
                    this.Kind = VisceralObjectKind.None;
                    return false;
                }
            }
            else if (this.Kind == VisceralObjectKind.Property)
            {// Property
            }

            return true;
        }

        public bool Initialize(VisceralBody<T> body, Type type, string fullName)
        {
            this.Body = body;
            this.type = type;
            this.FullName = fullName;
            this.Kind = this.TypeToObjectKind(this.type);

            if (this.Kind == VisceralObjectKind.None)
            {
                return false;
            }

            var primitiveType = VisceralHelper.Primitives_ShortenName(fullName);
            if (primitiveType != null)
            {// Primitive type
                this.IsPrimitive = true;
                this.BaseObject = null;
                this.AllMembers = ImmutableArray<T>.Empty;
                this.AllAttributes = ImmutableArray<VisceralAttribute>.Empty;
                return true;
            }

            return true;
        }

        public bool Initialize(VisceralBody<T> body, MemberInfo memberInfo)
        {
            this.Body = body;
            this.memberInfo = memberInfo;
            this.FullName = VisceralHelper.MemberInfoToFullName(this.memberInfo);
            this.Kind = this.MemberInfoToObjectType(this.memberInfo);
            if (this.Kind == VisceralObjectKind.None)
            {
                return false;
            }
            else if (this.Kind.IsType())
            {// Type
                return this.Initialize(body, (Type)memberInfo, this.FullName);
            }

            if (this.Kind == VisceralObjectKind.Method && this.memberInfo is MethodBase mb)
            {// Method
                if (memberInfo.MemberType != MemberTypes.Constructor && mb.IsSpecialName)
                {// Setter or Getter method.
                    this.Kind = VisceralObjectKind.None;
                    return false;
                }
            }
            else if (this.Kind == VisceralObjectKind.Field && this.memberInfo is FieldInfo fi)
            {// Field
                if (fi.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                {// Backing field.
                    this.Kind = VisceralObjectKind.None;
                    return false;
                }
            }
            else if (this.Kind == VisceralObjectKind.Property)
            {// Property
            }

            return true;
        }

        public IEnumerable<T> GetMembers(string simpleName)
        {
            return this.AllMembers.Where(x => x.SimpleName == simpleName);
        }

        public IEnumerable<T> GetMembers(VisceralTarget target = VisceralTarget.All)
        {
            return this.AllMembers.Where(x => this.CheckTarget(x.Kind, target));
        }

        public IEnumerable<T> GetMembers(
            VisceralTarget target = VisceralTarget.All,
            string? containingAttribute = null,
            string? containingInterface = null)
        {
            return this.AllMembers.Where(x =>
            {
                if (!this.CheckTarget(x.Kind, target))
                {
                    return false;
                }

                if (containingAttribute != null && !x.AllAttributes.Any(y => y.FullName == containingAttribute))
                {
                    return false;
                }

                if (containingInterface != null && !x.AllInterfaces.Any(y => y == containingInterface))
                {
                    return false;
                }

                return true;
            });
        }

        public VisceralBody<T> Body { get; private set; } = default!;

        public VisceralObjectKind Kind { get; private set; }

        private string? simpleName;

        public string SimpleName
        {// Simple name without namespace, parameters, arguments.
            // name, Int32, Class
            get
            {
                if (this.simpleName == null)
                {
                    if (this.symbol != null)
                    {
                        this.simpleName = this.Body.SymbolToSimpleName(this.symbol);
                    }
                    else if (this.type != null)
                    {
                        this.simpleName = VisceralHelper.TypeToSimpleName(this.type);
                    }
                    else if (this.memberInfo != null)
                    {
                        this.simpleName = VisceralHelper.MemberInfoToSimpleName(this.memberInfo);
                    }

                    if (this.simpleName == null)
                    {
                        this.simpleName = string.Empty;
                    }
                }

                return this.simpleName;
            }

            protected set
            {
                this.simpleName = value;
            }
        }

        private string? localName;

        public string LocalName
        {// SimpleName + Generics/Parameters
            // name, Int32, Class<String>
            get
            {
                if (this.localName == null)
                {
                    if (this.symbol != null)
                    {
                        this.localName = this.Body.SymbolToLocalName(this.symbol);
                    }
                    else if (this.type != null)
                    {
                        this.localName = VisceralHelper.TypeToLocalName(this.type);
                    }
                    else if (this.memberInfo != null)
                    {
                        this.localName = VisceralHelper.MemberInfoToLocalName(this.memberInfo);
                    }

                    if (this.localName == null)
                    {
                        this.localName = string.Empty;
                    }
                }

                return this.localName;
            }

            protected set
            {
                this.localName = value;
            }
        }

        private string? regionalName;

        public string RegionalName
        {// regional name of the type. Class+LocalName
            // class.name, Int32, class.Method(String)
            get
            {
                if (this.regionalName == null)
                {
                    if (this.symbol != null)
                    {
                        this.regionalName = this.Body.SymbolToRegionalName(this.symbol);
                    }
                    else if (this.type != null)
                    {
                        this.regionalName = VisceralHelper.TypeToFullName(this.type);
                    }
                    else if (this.memberInfo != null)
                    {
                        this.regionalName = VisceralHelper.MemberInfoToFullName(this.memberInfo);
                    }

                    if (this.regionalName == null)
                    {
                        this.regionalName = string.Empty;
                    }
                }

                return this.regionalName;
            }

            protected set
            {
                this.regionalName = value;
            }
        }

        private string? fullName;

        public string FullName
        {// Fully qualified name of the type. Namespace.Class+LocalName
            // Namespace.class.name, System.Int32, class.Method(System.String)
            get
            {
                if (this.fullName == null)
                {
                    if (this.symbol != null)
                    {
                        this.fullName = this.Body.SymbolToFullName(this.symbol);
                    }
                    else if (this.type != null)
                    {
                        this.fullName = VisceralHelper.TypeToFullName(this.type);
                    }
                    else if (this.memberInfo != null)
                    {// FullName property is already set by Initialize().
                    }

                    if (this.fullName == null)
                    {
                        this.fullName = string.Empty;
                    }
                }

                return this.fullName;
            }

            protected set
            {
                this.fullName = value;
            }
        }

        private string? @namespace;

        public string Namespace
        {// Namespace
            get
            {
                if (this.@namespace == null)
                {
                    if (this.symbol != null)
                    {
                        this.@namespace = this.symbol.ContainingNamespace.ToDisplayString();
                    }
                    else if (this.type != null)
                    {
                        this.@namespace = this.type.Namespace;
                    }
                    else if (this.memberInfo != null)
                    {
                        this.@namespace = this.memberInfo.DeclaringType.Namespace;
                    }

                    if (this.@namespace == null)
                    {
                        this.@namespace = string.Empty;
                    }
                }

                return this.@namespace;
            }

            protected set
            {
                this.@namespace = value;
            }
        }

        private ImmutableArray<VisceralAttribute> allAttributes;

        public ImmutableArray<VisceralAttribute> AllAttributes
        {
            get
            {
                if (this.allAttributes.IsDefault)
                {
                    if (this.IsSystem)
                    {
                        this.allAttributes = ImmutableArray<VisceralAttribute>.Empty;
                    }
                    else if (this.symbol != null)
                    {
                        this.allAttributes = this.SymbolToAttribute(this.symbol);
                    }
                    else if (this.type != null)
                    {
                        this.allAttributes = this.TypeToAttribute(CustomAttributeData.GetCustomAttributes(this.type));
                    }
                    else if (this.memberInfo != null)
                    {
                        this.allAttributes = this.TypeToAttribute(CustomAttributeData.GetCustomAttributes(this.memberInfo));
                    }
                    else
                    {
                        this.allAttributes = ImmutableArray<VisceralAttribute>.Empty;
                    }
                }

                return this.allAttributes;
            }

            protected set
            {
                this.allAttributes = value;
            }
        }

        private ImmutableArray<T> allMembers;

        public ImmutableArray<T> AllMembers
        {
            get
            {
                if (this.allMembers.IsDefault)
                {
                    if (this.IsSystem)
                    {
                        this.allMembers = ImmutableArray<T>.Empty;
                    }
                    else if (this.symbol is ITypeSymbol typeSymbol)
                    {
                        var builder = ImmutableArray.CreateBuilder<T>();
                        foreach (var x in typeSymbol.GetBaseTypesAndThis().SelectMany(x => x.GetMembers()))
                        {
                            if (x is IMethodSymbol ms)
                            {
                                if (ms.MethodKind == MethodKind.Constructor)
                                {
                                    if (this.Kind == VisceralObjectKind.Struct && ms.IsImplicitlyDeclared)
                                    {
                                        continue;
                                    }
                                }

                                if (ms.ContainingNamespace.Name == "System")
                                {
                                    continue;
                                }
                            }

                            if (this.Kind == VisceralObjectKind.Enum)
                            {
                                if (x is IMethodSymbol ms2 && ms2.MethodKind == MethodKind.Constructor)
                                {
                                    continue;
                                }
                                else if (x.Name == "EnumInfo")
                                {
                                    continue;
                                }
                            }

                            var symbol = x;
                            if (symbol is INamedTypeSymbol ts)
                            {
                                symbol = ts.OriginalDefinition;
                            }

                            var obj = this.Body.Add(symbol);
                            if (obj != null && obj.Kind != VisceralObjectKind.None)
                            { // Success.
                                builder.Add(obj);
                            }
                        }

                        this.allMembers = builder.ToImmutable();
                    }
                    else if (this.type != null)
                    {
                        var builder = ImmutableArray.CreateBuilder<T>();
                        foreach (var x in this.type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                        {
                            if (this.Kind == VisceralObjectKind.Enum)
                            {
                                if (x is FieldInfo fi && fi.IsSpecialName)
                                {// "value__"
                                    continue;
                                }
                            }

                            var obj = this.Body.Add(x);
                            if (obj != null && obj.Kind != VisceralObjectKind.None)
                            { // Success.
                                builder.Add(obj);
                            }
                        }

                        this.allMembers = builder.ToImmutable();
                    }
                    else
                    {
                        this.allMembers = ImmutableArray<T>.Empty;
                    }
                }

                return this.allMembers;
            }

            protected set
            {
                this.allMembers = value;
            }
        }

        private ImmutableArray<string> allInterfaces;

        public ImmutableArray<string> AllInterfaces
        {
            get
            {
                if (this.allInterfaces.IsDefault)
                {
                    if (this.IsSystem)
                    {
                        this.allInterfaces = ImmutableArray<string>.Empty;
                    }
                    else
                    if (this.symbol is ITypeSymbol typeSymbol)
                    {
                        var builder = ImmutableArray.CreateBuilder<string>();
                        foreach (var x in typeSymbol.AllInterfaces)
                        {
                            builder.Add(this.Body.SymbolToFullName(x));
                        }

                        this.allInterfaces = builder.ToImmutable();
                    }
                    else if (this.type != null)
                    {
                        var builder = ImmutableArray.CreateBuilder<string>();
                        foreach (var x in this.type.GetInterfaces())
                        {
                            builder.Add(VisceralHelper.TypeToFullName(x));
                        }

                        this.allInterfaces = builder.ToImmutable();
                    }
                    else
                    {
                        this.allInterfaces = ImmutableArray<string>.Empty;
                    }
                }

                return this.allInterfaces;
            }

            protected set
            {
                this.allInterfaces = value;
            }
        }

        private bool baseObjectFlag;
        private T? baseObject;

        public T? BaseObject
        {// Base type object of this type (class/struct).
            get
            {
                if (!this.baseObjectFlag)
                {// Try to aquire a base type.
                    this.baseObjectFlag = true;

                    if (this.IsSystem)
                    {
                        return null;
                    }

                    if (this.symbol != null)
                    {// Symbol
                        if (this.symbol is ITypeSymbol typeSymbol && typeSymbol.BaseType is { } baseType)
                        {// Type
                            if (baseType.SpecialType == SpecialType.System_Object)
                            {
                                return null;
                            }

                            this.baseObject = this.Body.Add(baseType);
                        }
                    }
                    else if (this.type != null)
                    {// Type
                        var type = this.type.BaseType;
                        if (type == typeof(object))
                        {
                            return null;
                        }

                        this.baseObject = this.Body.Add(type);
                    }
                }

                return this.baseObject;
            }

            protected set
            {
                this.baseObjectFlag = true;
                this.baseObject = value;
            }
        }

        private bool containingObjectFlag;
        private T? containingObject;

        public T? ContainingObject
        {// Containing type object of this type (class/struct).
            get
            {
                if (!this.containingObjectFlag)
                {// Try to aquire a containing type.
                    this.containingObjectFlag = true;

                    if (this.symbol?.ContainingType != null)
                    {// Symbol
                        this.containingObject = this.Body.Add(this.symbol.ContainingType);
                    }
                    else if (this.type?.DeclaringType != null)
                    {// Type
                        this.containingObject = this.Body.Add(this.type.DeclaringType);
                    }
                }

                return this.containingObject;
            }

            protected set
            {
                this.containingObjectFlag = true;
                this.containingObject = value;
            }
        }

        private Type? typeCache = typeof(object);

        public Type? Type
        {// The type of a Field/Property.
            get
            {
                if (this.typeCache == typeof(object))
                {// Try to aquire a base type.
                    this.typeCache = null;

                    if (this.IsPrimitive)
                    {
                    }
                    else if (this.symbol != null)
                    {// Symbol
                        string? fullName = null;
                        if (this.Kind.IsType())
                        {// Type
                            fullName = this.FullName;
                        }
                        else if (this.symbol is IFieldSymbol fs)
                        {// Field
                            fullName = this.Body.SymbolToFullName(fs.Type);
                        }
                        else if (this.symbol is IPropertySymbol ps)
                        {// Property
                            fullName = this.Body.SymbolToFullName(ps.Type);
                        }

                        if (fullName != null)
                        {
                            this.typeCache = VisceralHelper.Primitives_FullNameToType(fullName);
                        }
                    }
                    else if (this.type != null)
                    {// Type
                        this.typeCache = this.type;
                        if (this.typeCache == typeof(object))
                        {
                            this.typeCache = null;
                        }
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.typeCache = fi.FieldType;
                    }
                    else if (this.memberInfo is PropertyInfo pi)
                    {// Property
                        this.typeCache = pi.PropertyType;
                    }
                }

                return this.typeCache;
            }

            protected set
            {
                this.typeCache = value;
            }
        }

        private bool typeObjectFlag;
        private T? typeObject;

        public T? TypeObject
        {// A type object. If this object is a type (Kind.IsType()), returns itself. Otherwise, gets a type object of this field/property.
            get
            {
                if (!this.typeObjectFlag)
                {
                    this.typeObjectFlag = true;

                    if (this.IsPrimitive)
                    {
                        this.typeObject = null;
                    }
                    else if (this.Kind.IsType())
                    { // Class, Struct
                        this.typeObject = (T)this;
                    }
                    else if (this.symbol is IFieldSymbol fs)
                    {// Field symbol
                        this.typeObject = this.Body.Add(fs.Type);
                    }
                    else if (this.symbol is IPropertySymbol ps)
                    {// Property symbol
                        this.typeObject = this.Body.Add(ps.Type);
                    }
                    else if (this.symbol is IMethodSymbol ms)
                    {
                        this.typeObject = this.Body.Add(ms.ReturnType);
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.typeObject = this.Body.Add(fi.FieldType);
                    }
                    else if (this.memberInfo is PropertyInfo pi)
                    {// Property
                        this.typeObject = this.Body.Add(pi.PropertyType);
                    }
                }

                return this.typeObject;
            }

            protected set
            {
                this.typeObjectFlag = true;
                this.typeObject = value;
            }
        }

        private bool typeObjectWithNullableFlag;
        private WithNullable<T>? typeObjectWithNullable;

        public WithNullable<T>? TypeObjectWithNullable
        {
            get
            {
                if (!this.typeObjectWithNullableFlag)
                {
                    this.typeObjectWithNullableFlag = true;

                    if (this.TypeObject == null)
                    {
                        return null;
                    }

                    if (this.symbol is ITypeSymbol ts)
                    {// Type symbol
                        this.typeObjectWithNullable = new WithNullable<T>(this.TypeObject, ts, ts.NullableAnnotation);
                    }
                    else if (this.symbol is IFieldSymbol fs)
                    {// Field symbol
                        this.typeObjectWithNullable = new WithNullable<T>(this.TypeObject, fs.Type, fs.NullableAnnotation);
                    }
                    else if (this.symbol is IPropertySymbol ps)
                    {// Property symbol
                        this.typeObjectWithNullable = new WithNullable<T>(this.TypeObject, ps.Type, ps.NullableAnnotation);
                    }
                }

                return this.typeObjectWithNullable;
            }

            protected set
            {
                this.typeObjectWithNullableFlag = true;
                this.typeObjectWithNullable = value;
            }
        }

        private bool originalDefinitionFlag;
        private T? originalDefinition;

        public T? OriginalDefinition
        {
            get
            {
                if (!this.originalDefinitionFlag)
                {// Try to aquire an object.
                    this.originalDefinitionFlag = true;

                    if (this.IsPrimitive)
                    {
                        return null;
                    }

                    if (this.symbol != null)
                    {// Symbol
                        if (this.symbol is INamedTypeSymbol ts && ts.OriginalDefinition is { } cs)
                        {// Type
                            if (cs.SpecialType == SpecialType.System_Object)
                            {
                                return null;
                            }

                            this.originalDefinition = this.Body.Add(cs);
                        }
                    }
                }

                return this.originalDefinition;
            }

            protected set
            {
                this.originalDefinitionFlag = true;
                this.originalDefinition = value;
            }
        }

        /*private bool constructedFromFlag;
        private T? constructedFrom;

        public T? ConstructedFrom
        {
            get
            {
                if (!this.constructedFromFlag)
                {// Try to aquire an object.
                    this.constructedFromFlag = true;

                    if (this.IsPrimitive)
                    {
                        return null;
                    }

                    if (this.symbol != null)
                    {// Symbol
                        if (this.symbol is INamedTypeSymbol ts && ts.ConstructedFrom is { } cs)
                        {// Type
                            if (cs.SpecialType == SpecialType.System_Object)
                            {
                                return null;
                            }

                            this.constructedFrom = this.Body.Add(cs);
                        }
                    }
                }

                return this.constructedFrom;
            }

            protected set
            {
                this.constructedFromFlag = true;
                this.constructedFrom = value;
            }
        }*/

        private bool? isSystem;

        public bool IsSystem
        {
            get
            {
                if (this.isSystem == null)
                {
                    if (this.IsPrimitive)
                    {
                        this.isSystem = true;
                        return true;
                    }

                    /* var baseObject = this.BaseObject;
                    while (baseObject != null)
                    {
                        if (baseObject.IsSystem)
                        {
                            this.isSystem = true;
                            return true;
                        }

                        baseObject = baseObject.BaseObject;
                    }*/

                    if (this.symbol is ITypeSymbol ts && (ts.TypeKind == TypeKind.Array || ts.SpecialType == SpecialType.System_Array || ts.SpecialType == SpecialType.System_ValueType || ts.SpecialType == SpecialType.System_Enum))
                    {
                        this.isSystem = true;
                    }
                    else if (this.type?.IsArray == true || this.type == typeof(System.Array) || this.type == typeof(System.ValueType) || this.type == typeof(System.Enum))
                    {
                        this.isSystem = true;
                    }
                    else if (this.IsTuple)
                    {
                        this.isSystem = true;
                    }
                    else
                    {
                        this.isSystem = false;
                    }
                }

                return this.isSystem ?? false;
            }

            protected set
            {
                this.isSystem = value;
            }
        }

        public bool IsPrimitive { get; protected set; }

        public bool IsTuple
        {
            get
            {
                if (this.symbol is ITypeSymbol ts)
                {
                    return ts.IsTupleType;
                }
                else if (this.type?.IsTuple() == true)
                {
                    return true;
                }

                return false;
            }
        }

        private bool? isPartial;

        public bool IsPartial
        {
            get
            {
                if (this.isPartial == null)
                {
                    this.isPartial = false;

                    // Determines wether the class is a partial class.
                    if (this.Kind.IsType())
                    {
                        if (this.symbol != null)
                        {
                            foreach (var x in this.symbol.DeclaringSyntaxReferences)
                            {
                                if (x.GetSyntax() is TypeDeclarationSyntax syntax)
                                {
                                    if (syntax.Modifiers.Any(a => a.Kind() == SyntaxKind.PartialKeyword))
                                    {
                                        this.isPartial = true;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (this.type != null)
                        {// Type
                        }
                    }
                }

                return this.isPartial ?? false;
            }

            protected set
            {
                this.isPartial = value;
            }
        }

        public bool IsConstructedFromNullable
        {
            get
            {
                if (this.symbol is INamedTypeSymbol ts)
                {
                    return ts.ConstructedFrom.ContainingNamespace.Name == "System" &&
                        ts.ConstructedFrom.Name == "Nullable";
                }
                else if (this.type != null && this.type.IsGenericType)
                {
                    var t = this.type.GetGenericTypeDefinition();
                    return t == typeof(Nullable<>);
                }

                return false;
            }
        }

        private bool? isPublic;

        public bool IsPublic
        {// Field is public / Property's getter and setter is both public.
            get
            {
                if (this.isPublic == null)
                {
                    if (this.symbol != null)
                    {
                        this.isPublic = (this.symbol.DeclaredAccessibility == Accessibility.Public) || (this.symbol.DeclaredAccessibility == Accessibility.NotApplicable);
                        if (this.symbol is IPropertySymbol ps)
                        {
                            if (ps.GetMethod is IMethodSymbol m)
                            {
                                if (m.DeclaredAccessibility != Accessibility.Public)
                                {
                                    this.isPublic = false;
                                }
                            }
                            else
                            {
                                this.isPublic = false;
                            }

                            if (ps.SetMethod is IMethodSymbol m2)
                            {
                                if (m2.DeclaredAccessibility != Accessibility.Public)
                                {
                                    this.isPublic = false;
                                }
                            }
                            else
                            {
                                this.isPublic = false;
                            }
                        }
                    }
                    else if (this.type != null)
                    {// Type
                        this.isPublic = this.type.IsPublic || this.type.IsNestedPublic;
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.isPublic = fi.IsPublic;
                    }
                    else if (this.memberInfo is MethodBase mb)
                    {// Method
                        this.isPublic = mb.IsPublic;
                    }
                    else if (this.memberInfo is PropertyInfo pi)
                    {// Property
                        this.isPublic = pi.SetMethod?.IsPublic == true && pi.GetMethod?.IsPublic == true;
                    }
                }

                return this.isPublic ?? false;
            }

            protected set
            {
                this.isPublic = value;
            }
        }

        private bool? isReadOnly;

        public bool IsReadOnly
        {// Indicating that Field/Property is readonly.
            get
            {
                if (this.isReadOnly == null)
                {
                    if (this.symbol != null)
                    {
                        if (this.symbol is IFieldSymbol fs)
                        {
                            this.isReadOnly = fs.IsReadOnly;
                        }
                        else if (this.symbol is IPropertySymbol ps)
                        {
                            this.isReadOnly = ps.IsReadOnly;
                        }
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.isReadOnly = fi.IsInitOnly;
                    }
                    else if (this.memberInfo is PropertyInfo pi)
                    {// Property
                        this.isReadOnly = !pi.CanWrite;
                    }
                }

                return this.isReadOnly ?? false;
            }

            protected set
            {
                this.isReadOnly = value;
            }
        }

        private bool? isReadable;

        public bool IsReadable
        {// Field/Property is readable. Accessibility is not concerned.
            get
            {
                if (this.isReadable == null)
                {
                    if (this.symbol != null)
                    {
                        if (this.symbol is IFieldSymbol fs)
                        {
                            this.isReadable = true;
                        }
                        else if (this.symbol is IPropertySymbol ps)
                        {
                            this.isReadable = !ps.IsWriteOnly;
                        }
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.isReadable = true;
                    }
                    else if (this.memberInfo is PropertyInfo pi)
                    {// Property
                        this.isReadable = pi.CanRead;
                    }
                }

                return this.isReadable ?? false;
            }

            protected set
            {
                this.isReadable = value;
            }
        }

        private bool? isWritable;

        public bool IsWritable
        {// Field/Property is writable. Accessibility is not concerned.
            get
            {
                if (this.isWritable == null)
                {
                    if (this.symbol != null)
                    {
                        if (this.symbol is IFieldSymbol fs)
                        {
                            this.isWritable = !fs.IsReadOnly && !fs.IsConst;
                        }
                        else if (this.symbol is IPropertySymbol ps)
                        {
                            this.isWritable = !ps.IsReadOnly;
                        }
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.isWritable = !fi.IsInitOnly && !fi.IsLiteral;
                    }
                    else if (this.memberInfo is PropertyInfo pi)
                    {// Property
                        this.isWritable = pi.CanWrite;
                    }
                }

                return this.isWritable ?? false;
            }

            protected set
            {
                this.isWritable = value;
            }
        }

        private bool? isSerializable;

        public bool IsSerializable
        {
            get
            {
                if (this.isSerializable == null)
                {
                    this.isSerializable = this.IsReadable && this.IsWritable;
                }

                return this.isSerializable ?? false;
            }

            protected set
            {
                this.isSerializable = value;
            }
        }

        private bool? isStatic;

        public bool IsStatic
        {
            get
            {
                if (this.isStatic == null)
                {
                    if (this.symbol is { } s)
                    {
                        this.isStatic = s.IsStatic;
                    }
                    else if (this.memberInfo is FieldInfo fi)
                    {// Field
                        this.isStatic = fi.IsStatic;
                    }
                    else
                    {
                        this.isStatic = false;
                    }
                }

                return this.isStatic ?? false;
            }

            protected set
            {
                this.isStatic = value;
            }
        }

        public string KindName => this.Kind switch
        {
            VisceralObjectKind.Class => "class",
            VisceralObjectKind.Interface => "interface",
            VisceralObjectKind.Struct => "struct",
            VisceralObjectKind.Record => "record",
            VisceralObjectKind.Field => "field",
            VisceralObjectKind.Property => "property",
            VisceralObjectKind.Method => "method",
            _ => string.Empty,
        };

        public string AccessibilityName
        {
            get
            {
                if (this.IsSystem)
                {
                    return string.Empty;
                }
                else if (this.symbol != null)
                {
                    if (this.symbol is IPropertySymbol ps)
                    {
                        var getAccessibility = Accessibility.NotApplicable;
                        if (ps.GetMethod is IMethodSymbol getMethod)
                        {
                            getAccessibility = getMethod.DeclaredAccessibility;
                        }

                        var setAccessibility = Accessibility.NotApplicable;
                        if (ps.SetMethod is IMethodSymbol setMethod)
                        {
                            setAccessibility = setMethod.DeclaredAccessibility;
                        }

                        var min = getAccessibility < setAccessibility ? getAccessibility : setAccessibility;
                        return VisceralHelper.AccessibilityToString(min);
                    }
                    else
                    {
                        return this.symbol.DeclaredAccessibility.AccessibilityToString();
                    }
                }
                else if (this.type != null)
                {
                    if (!this.type.IsNested)
                    {
                        if (this.type.IsPublic)
                        {
                            return "public";
                        }
                        else if (!this.type.IsVisible)
                        {
                            return "internal";
                        }
                    }
                    else
                    {// Nested.
                        if (this.type.IsNestedPublic)
                        {
                            return "public";
                        }
                        else if (this.type.IsNestedPrivate)
                        {
                            return "private";
                        }
                        else if (this.type.IsNestedFamily)
                        {
                            return "protected";
                        }
                        else if (this.type.IsNestedFamORAssem)
                        {
                            return "protected internal";
                        }
                        else if (this.type.IsNestedFamANDAssem)
                        {
                            return "private protected";
                        }
                    }
                }
                else if (this.memberInfo is FieldInfo fi)
                {// Field
                    if (fi.IsPrivate)
                    {
                        return "private";
                    }
                    else if (fi.IsFamilyAndAssembly)
                    {
                        return "private protected";
                    }
                    else if (fi.IsFamily)
                    {
                        return "protected";
                    }
                    else if (fi.IsAssembly)
                    {
                        return "internal"
;
                    }
                    else if (fi.IsFamilyOrAssembly)
                    {
                        return "protected internal";
                    }
                    else if (fi.IsPublic)
                    {
                        return "public";
                    }
                }
                else if (this.memberInfo is PropertyInfo pi)
                {// Property
                    return VisceralHelper.PropertyInfoToAccessibilityName(pi);
                }
                else if (this.memberInfo is MethodBase mb)
                {// Method
                    return VisceralHelper.MethodBaseToAccessibility(mb).AccessibilityToString();
                }

                return string.Empty;
            }
        }

        public Location Location
        {
            get
            {
                if (this.symbol != null)
                {
                    var first = this.symbol.Locations.First();
                    if (first != null)
                    {
                        return first;
                    }
                }

                return Location.None;
            }
        }

        public bool Generics_IsGeneric
        {
            get
            {
                if (this.symbol is INamedTypeSymbol ts)
                {
                    return ts.IsGenericType;
                }
                else if (this.type != null)
                {
                    return this.type.IsGenericType;
                }

                return false;
            }
        }

        private VisceralGenericsKind generics_Kind;

        public VisceralGenericsKind Generics_Kind
        {
            get
            {
                if (this.generics_Kind != VisceralGenericsKind.NotSet)
                {
                    return this.generics_Kind;
                }

                if (this.symbol != null)
                {
                    this.generics_Kind = VisceralHelper.TypeToGenericsKind(this.symbol);
                }

                /*if (this.symbol is INamedTypeSymbol ts)
                {
                    if (!ts.IsGenericType)
                    {
                        this.generics_Kind = VisceralGenericsKind.NotGeneric;
                    }
                    else if (ts.IsUnboundGenericType)// || ts.IsDefinition
                    {
                        this.generics_Kind = VisceralGenericsKind.OpenGeneric; // VisceralGenericsKind.UnboundGeneric;
                    }
                    else
                    {
                        var c = this.ContainingObject;
                        while (c != null)
                        {
                            if (c.Generics_Kind == VisceralGenericsKind.OpenGeneric)
                            {
                                this.generics_Kind = VisceralGenericsKind.OpenGeneric;
                                return this.generics_Kind;
                            }

                            c = c.ContainingObject;
                        }

                        foreach (var x in ts.TypeArguments)
                        {
                            if (x.Kind == SymbolKind.TypeParameter)
                            {
                                this.generics_Kind = VisceralGenericsKind.OpenGeneric;
                                return this.generics_Kind;
                            }
                        }

                        this.generics_Kind = VisceralGenericsKind.CloseGeneric;
                    }
                }*/
                else if (this.type != null)
                {
                    if (!this.type.IsGenericType)
                    {
                        this.generics_Kind = VisceralGenericsKind.NotGeneric;
                    }
                    else if (this.type.IsGenericTypeDefinition)
                    {
                        this.generics_Kind = VisceralGenericsKind.OpenGeneric; // VisceralGenericsKind.UnboundGeneric;
                    }
                    else if (this.type.ContainsGenericParameters)
                    {
                        this.generics_Kind = VisceralGenericsKind.OpenGeneric;
                    }
                    else
                    {
                        this.generics_Kind = VisceralGenericsKind.CloseGeneric;
                    }
                }
                else
                {
                    this.generics_Kind = VisceralGenericsKind.NotGeneric;
                }

                return this.generics_Kind;
            }
        }

        private ImmutableArray<T> genericsArguments;

        public ImmutableArray<T> Generics_Arguments
        {
            get
            {
                if (this.genericsArguments.IsDefault)
                {
                    if (this.symbol is INamedTypeSymbol ts)
                    {
                        var builder = ImmutableArray.CreateBuilder<T>();
                        foreach (var x in ts.TypeArguments)
                        {
                            var t = this.Body.Add(x);
                            if (t != null)
                            {
                                builder.Add(t);
                            }
                        }

                        this.genericsArguments = builder.ToImmutable();
                    }
                    else if (this.type?.IsGenericType == true)
                    {
                        var builder = ImmutableArray.CreateBuilder<T>();

                        var declaringCount = this.type.DeclaringType == null ? 0 : this.type.DeclaringType.GetGenericArguments().Length;
                        var genericArguments = this.type.GetGenericArguments();
                        for (var i = declaringCount; i < genericArguments.Length; i++)
                        {
                            var t = this.Body.Add(genericArguments[i]);
                            if (t != null)
                            {
                                builder.Add(t);
                            }
                        }

                        this.genericsArguments = builder.ToImmutable();
                    }
                    else
                    {
                        this.genericsArguments = ImmutableArray<T>.Empty;
                    }
                }

                return this.genericsArguments;
            }

            protected set
            {
                this.genericsArguments = value;
            }
        }

        public bool Method_IsConstructor
        {
            get
            {
                if (this.symbol is IMethodSymbol ms)
                {
                    return ms.MethodKind == MethodKind.Constructor;
                }
                else if (this.memberInfo is MethodBase mb)
                {
                    return mb.IsConstructor;
                }

                return false;
            }
        }

        private ImmutableArray<string> methodParameters;

        public ImmutableArray<string> Method_Parameters
        {
            get
            {
                if (this.methodParameters.IsDefault)
                {
                    if (this.symbol is IMethodSymbol ms)
                    {
                        var builder = ImmutableArray.CreateBuilder<string>();
                        foreach (var x in ms.Parameters)
                        {
                            builder.Add(this.Body.SymbolToFullName(x.Type));
                        }

                        this.methodParameters = builder.ToImmutable();
                    }
                    else if (this.memberInfo is MethodBase mb)
                    {
                        var builder = ImmutableArray.CreateBuilder<string>();
                        foreach (var x in mb.GetParameters())
                        {
                            builder.Add(VisceralHelper.TypeToFullName(x.ParameterType));
                        }

                        this.methodParameters = builder.ToImmutable();
                    }
                    else
                    {
                        this.methodParameters = ImmutableArray<string>.Empty;
                    }
                }

                return this.methodParameters;
            }

            protected set
            {
                this.methodParameters = value;
            }
        }

        public T? Array_Element
        {
            get
            {
                if (this.symbol is IArrayTypeSymbol ats && ats.ElementType is { } t)
                {
                    if (t.SpecialType == SpecialType.System_Object)
                    {
                        return null;
                    }

                    return this.Body.Add(t);
                }
                else if (this.type != null && this.type.IsArray)
                {
                    return this.Body.Add(this.type.GetElementType());
                }
                else
                {
                    return null;
                }
            }
        }

        public int Array_Rank
        {
            get
            {
                if (this.symbol is IArrayTypeSymbol ats)
                {
                    return ats.Rank;
                }
                else if (this.type != null && this.type.IsArray)
                {
                    return this.type.GetArrayRank();
                }
                else
                {
                    return 0;
                }
            }
        }

        public T? Enum_UnderlyingTypeObject
        {
            get
            {
                if (this.symbol is INamedTypeSymbol ts && ts.EnumUnderlyingType is { } t)
                {
                    if (t.SpecialType == SpecialType.System_Object)
                    {
                        return null;
                    }

                    return this.Body.Add(t);
                }
                else if (this.type != null && this.type.IsEnum)
                {
                    return this.Body.Add(this.type.GetEnumUnderlyingType());
                }
                else
                {
                    return null;
                }
            }
        }

        public T? Enum_GetEnumObjectFromObject(object targetObject)
        {
            if (this.Kind != VisceralObjectKind.Enum)
            {// Only Enum
                return null;
            }

            // var target = Convert.ToInt32(obj);
            foreach (var x in this.AllMembers)
            {
                if (x.symbol is IFieldSymbol fs)
                {
                    if (fs.HasConstantValue && fs.ConstantValue == targetObject)
                    {
                        return x;
                    }
                }
            }

            return null;
        }

        public WithNullable<T>? CreateWithNullable(NullableAnnotation nullableAnnotation)
        {
            if (this.symbol == null)
            {
                return null;
            }

            if (this.symbol is ITypeSymbol ts)
            {// Type symbol
                return new WithNullable<T>((T)this, ts, nullableAnnotation);
            }

            return null;
        }

        protected ISymbol? symbol; // Either symbol (Source Generator) or type (Reflection) is valid.
        protected Type? type;
        protected MemberInfo? memberInfo;

        public void GetRawInformation(out ISymbol? symbol, out Type? type, out MemberInfo? memberInfo)
        {
            symbol = this.symbol;
            type = this.type;
            memberInfo = this.memberInfo;
        }

        public bool IsDerivedOrImplementing(T target)
        {
            if (target.Kind == VisceralObjectKind.Interface)
            {// Interface
                return this.AllInterfaces.Any(x => x == target.FullName);
            }
            else
            {// Other
                T? t = (T)this;
                while (t != null)
                {
                    if (t == target)
                    {
                        return true;
                    }

                    t = t.BaseObject;
                }

                return false;
            }
        }

        private ImmutableArray<VisceralAttribute> SymbolToAttribute(ISymbol symbol)
        {
            var builder = ImmutableArray.CreateBuilder<VisceralAttribute>();
            foreach (var x in symbol.GetAttributes())
            {
                var name = x.AttributeClass != null ? this.Body.SymbolToFullName(x.AttributeClass) : null;
                if (name != null && name != string.Empty)
                {
                    if (name.StartsWith("System.Runtime.CompilerServices"))
                    {
                        continue;
                    }

                    builder.Add(new VisceralAttribute(name, x));
                }
            }

            return builder.ToImmutable();
        }

        private ImmutableArray<VisceralAttribute> TypeToAttribute(IList<CustomAttributeData> attributes)
        {
            var builder = ImmutableArray.CreateBuilder<VisceralAttribute>();
            foreach (var x in attributes)
            {
                var name = VisceralHelper.TypeToFullName(x.AttributeType);
                if (name.StartsWith("System.Runtime.CompilerServices"))
                {
                    continue;
                }

                builder.Add(new VisceralAttribute(name, x));
            }

            return builder.ToImmutable();
        }

        private VisceralObjectKind MemberInfoToObjectType(MemberInfo memberInfo) => memberInfo.MemberType switch
        {
            MemberTypes.Constructor => VisceralObjectKind.Method,
            MemberTypes.Field => VisceralObjectKind.Field,
            MemberTypes.Method => VisceralObjectKind.Method,
            MemberTypes.NestedType => this.TypeToObjectKind((Type)memberInfo),
            MemberTypes.Property => VisceralObjectKind.Property,
            MemberTypes.TypeInfo => this.TypeToObjectKind((Type)memberInfo),
            _ => VisceralObjectKind.None,
        };

        private VisceralObjectKind TypeToObjectKind(Type type)
        {
            if (type.IsGenericParameter)
            {
                return VisceralObjectKind.TypeParameter;
            }
            else if (type.IsClass())
            {
                return VisceralObjectKind.Class;
            }
            else if (type.IsStruct())
            {
                return VisceralObjectKind.Struct;
            }
            else if (type.IsPrimitive)
            {
                return VisceralObjectKind.Struct;
            }
            else if (type.IsEnum)
            {
                return VisceralObjectKind.Enum;
            }
            else if (type.IsInterface)
            {
                return VisceralObjectKind.Interface;
            }

            return VisceralObjectKind.None;
        }

        private VisceralObjectKind ISymbolToObjectKind(ISymbol symbol) => symbol switch
        {
            ITypeSymbol ts => this.ITypeSymbolToObjectKind(ts),
            IFieldSymbol => VisceralObjectKind.Field,
            IPropertySymbol => VisceralObjectKind.Property,
            IMethodSymbol => VisceralObjectKind.Method,
            _ => VisceralObjectKind.None,
        };

        private bool CheckTarget(VisceralObjectKind type, VisceralTarget target) => type switch
        {
            VisceralObjectKind.Class => (target & VisceralTarget.Class) != 0,
            VisceralObjectKind.Interface => (target & VisceralTarget.Interface) != 0,
            VisceralObjectKind.Struct => (target & VisceralTarget.Struct) != 0,
            VisceralObjectKind.Record => (target & VisceralTarget.Record) != 0,
            VisceralObjectKind.Field => (target & VisceralTarget.Field) != 0,
            VisceralObjectKind.Property => (target & VisceralTarget.Property) != 0,
            VisceralObjectKind.Method => (target & VisceralTarget.Method) != 0,
            _ => false,
        };

        private VisceralObjectKind ITypeSymbolToObjectKind(ITypeSymbol symbol) => symbol.TypeKind switch
        {
            TypeKind.Class => VisceralObjectKind.Class,
            TypeKind.Interface => VisceralObjectKind.Interface,
            TypeKind.Struct => VisceralObjectKind.Struct,
            TypeKind.TypeParameter => VisceralObjectKind.TypeParameter,
            TypeKind.Array => VisceralObjectKind.Class,
            TypeKind.Enum => VisceralObjectKind.Enum,
            _ => VisceralObjectKind.None,
        };
    }
}
