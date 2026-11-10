using System;

/// Declares that a node type leaves nothing changed in the world once it has run: audio, camera,
/// pure flow, on-screen text. Every node type must either describe its effect
/// (INarrativeStateEffect / INarrativeRouteEffect) or carry this attribute, so the state projection
/// can never miss a change by omission. Not inherited: a subclass has to declare it again.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NarrativeNoWorldEffectAttribute : Attribute { }
