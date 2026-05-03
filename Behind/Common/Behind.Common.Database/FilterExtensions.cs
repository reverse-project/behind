using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Text.RegularExpressions;
using LaYumba.Functional;
using static LaYumba.Functional.F;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Behind.Common.Database;

public static class FilterExtensions
{
    extension(string expression)
    {
        public Option<Filter<T>> Create<T>(DbContext context)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return Some((Filter<T>)new EmptyFilter<T>());
            }

            try
            {
                var config = new ParsingConfig();
                var additionalTypes = new[] { typeof(EF) };

                config.CustomTypeProvider = new DefaultDynamicLinqCustomTypeProvider(
                    config,
                    additionalTypes,
                    cacheCustomTypes: true
                );

                var entityType = context.Model.FindEntityType(typeof(T));
                if (entityType is null) return None;

                string processedFormula = PreProcessFormula(entityType, expression);

                var lambda = DynamicExpressionParser.ParseLambda<T, bool>(
                    config,
                    false,
                    processedFormula
                );

                return Some(new Filter<T>(lambda));
            }
            catch
            {
                return None;
            }
        }
    }

    extension(IEntityType entityType)
    {
        string PreProcessFormula(string expression)
        {
            var result = expression;

            foreach (var property in entityType.GetProperties())
            {
                string name = property.Name;
                string typeName = GetSafeTypeName(property.ClrType);

                var pattern = $@"\b{name}\b";
                var replacement = $"EF.Property<{typeName}>(it, \"{name}\")";

                result = Regex.Replace(result, pattern, replacement);
            }

            return result;
        }
    }

    extension(Type type)
    {
        string GetSafeTypeName()
        {
            var underlying = Nullable.GetUnderlyingType(type);

            if (underlying != null)
            {
                return $"{underlying.Name}?";
            }

            return type.Name switch
            {
                "Int32" => "int",
                "Int64" => "long",
                "Boolean" => "bool",
                "String" => "string",
                _ => type.Name
            };
        }
    }
}