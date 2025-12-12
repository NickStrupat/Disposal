using System.Linq.Expressions;
using System.Reflection;

namespace Disposal;

public static class FieldInfoExtensions
{
	public static Func<Object, Object?> CreateFieldGetter(this FieldInfo fieldInfo)
	{
		var targetParam = Expression.Parameter(typeof(Object), "target");
		var castedTarget = Expression.Convert(targetParam, fieldInfo.DeclaringType!);
		var fieldAccess = Expression.Field(castedTarget, fieldInfo);
		var castedFieldAccess = Expression.Convert(fieldAccess, typeof(Object));
		var lambda = Expression.Lambda<Func<Object, Object?>>(castedFieldAccess, targetParam);
		return lambda.Compile();
	}
}