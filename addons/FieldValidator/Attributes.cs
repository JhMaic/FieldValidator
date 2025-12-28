using System;

namespace FieldValidator;

/// <summary>
///     Checks if a reference is null, ensuring the field has been assigned in the Inspector.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MustSetAttribute : Attribute
{
}

/// <summary>
///     Checks for null elements within a collection, ensuring all array/list slots are properly initialized in the
///     Inspector.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MemberMustSetAttribute : Attribute
{
}