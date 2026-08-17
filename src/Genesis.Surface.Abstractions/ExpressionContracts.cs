using System.Text.Json.Serialization;

namespace Genesis.Surface.Abstractions;

/// <summary>Renderer-neutral UI expression intermediate representation.</summary>
public sealed record SurfaceExpression(
    string ExpressionId,
    string Name,
    string SourceKind,
    SurfaceNode Root,
    IReadOnlyList<SurfaceAction> Actions,
    IReadOnlyList<SurfaceBinding> Bindings,
    IReadOnlyList<SurfaceCapability> RequiredCapabilities,
    string Version = "1.0.0",
    DateTimeOffset? CreatedAt = null);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "nodeType")]
[JsonDerivedType(typeof(SurfaceContainerNode), "container")]
[JsonDerivedType(typeof(SurfaceTextNode), "text")]
[JsonDerivedType(typeof(SurfaceInputNode), "input")]
[JsonDerivedType(typeof(SurfaceActionNode), "action")]
public abstract record SurfaceNode(
    string NodeId,
    string Role,
    string? Label = null,
    string? Binding = null,
    string? ActionId = null,
    IReadOnlyList<SurfaceNode>? Children = null);

public sealed record SurfaceContainerNode(
    string NodeId,
    string Role,
    string Layout = "column",
    string? Label = null,
    IReadOnlyList<SurfaceNode>? Children = null)
    : SurfaceNode(NodeId, Role, Label, Children: Children);

public sealed record SurfaceTextNode(
    string NodeId,
    string Text,
    string? Label = null,
    string? Binding = null)
    : SurfaceNode(NodeId, "text", Label, Binding);

public sealed record SurfaceInputNode(
    string NodeId,
    string ValueType,
    string Binding,
    bool Required = false,
    string? Label = null,
    string? Placeholder = null)
    : SurfaceNode(NodeId, "input", Label, Binding);

public sealed record SurfaceActionNode(
    string NodeId,
    string ActionId,
    string Label,
    string Style = "primary")
    : SurfaceNode(NodeId, "action", Label, ActionId);

public sealed record SurfaceAction(
    string Id,
    string Label,
    string Description,
    string ReturnType,
    IReadOnlyList<SurfaceParameter> Parameters,
    string? Capability = null,
    bool MutatesData = false);

public sealed record SurfaceParameter(
    string Name,
    string Type,
    string Description,
    bool Required,
    string? DefaultValue = null);

public sealed record SurfaceBinding(
    string Path,
    string Mode = "twoWay",
    string? Format = null);

public sealed record SurfaceCapability(
    string Name,
    string Reason,
    bool RequiresApproval = true);

[JsonSerializable(typeof(SurfaceExpression))]
[JsonSerializable(typeof(SurfaceNode))]
[JsonSerializable(typeof(SurfaceContainerNode))]
[JsonSerializable(typeof(SurfaceTextNode))]
[JsonSerializable(typeof(SurfaceInputNode))]
[JsonSerializable(typeof(SurfaceActionNode))]
[JsonSerializable(typeof(SurfaceAction))]
[JsonSerializable(typeof(SurfaceParameter))]
[JsonSerializable(typeof(SurfaceBinding))]
[JsonSerializable(typeof(SurfaceCapability))]
public partial class SurfaceExpressionJsonContext : JsonSerializerContext { }
