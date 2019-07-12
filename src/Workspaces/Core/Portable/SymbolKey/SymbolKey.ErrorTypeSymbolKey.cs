﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis
{
    internal partial struct SymbolKey
    {
        private static class ErrorTypeSymbolKey
        {
            public static void Create(INamedTypeSymbol symbol, SymbolKeyWriter visitor)
            {
                visitor.WriteString(symbol.Name);
                visitor.WriteSymbolKey(symbol.ContainingSymbol as INamespaceOrTypeSymbol);
                visitor.WriteInteger(symbol.Arity);

                if (!symbol.Equals(symbol.ConstructedFrom))
                {
                    visitor.WriteSymbolArray(symbol.TypeArguments);
                }
                else
                {
                    visitor.WriteSymbolArray(ImmutableArray<ITypeSymbol>.Empty);
                }
            }

            public static SymbolKeyResolution Resolve(SymbolKeyReader reader)
            {
                var name = reader.ReadString();
                var containingSymbolResolution = reader.ReadSymbolKey();
                var arity = reader.ReadInteger();

                using var typeArguments = reader.ReadSymbolArray<ITypeSymbol>();
                if (typeArguments.Count != arity)
                {
                    return default;
                }

                using var errorTypes = CreateErrorTypes(reader, containingSymbolResolution, name, arity);
                if (arity == 0)
                {
                    return CreateSymbolInfo(errorTypes);
                }

                using var result = PooledArrayBuilder<INamedTypeSymbol>.GetInstance();
                var typeArgumentsArray = typeArguments.Builder.ToArray();
                foreach (var type in errorTypes)
                {
                    result.AddIfNotNull(type.Construct(typeArgumentsArray));
                }

                return CreateSymbolInfo(result);
            }

            private static PooledArrayBuilder<INamedTypeSymbol> CreateErrorTypes(
                SymbolKeyReader reader,
                SymbolKeyResolution containingSymbolResolution, string name, int arity)
            {
                var errorTypes = PooledArrayBuilder<INamedTypeSymbol>.GetInstance();

                foreach (var container in containingSymbolResolution)
                {
                    if (container is INamespaceOrTypeSymbol containerTypeOrNS)
                    {
                        errorTypes.AddIfNotNull(reader.Compilation.CreateErrorTypeSymbol(containerTypeOrNS, name, arity));
                    }
                }

                // Always ensure at least one error type was created.
                if (errorTypes.Count == 0)
                {
                    errorTypes.AddIfNotNull(reader.Compilation.CreateErrorTypeSymbol(null, name, arity));
                }

                return errorTypes;
            }
        }
    }
}
