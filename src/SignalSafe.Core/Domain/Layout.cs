namespace SignalSafe.Core.Domain;

/// <summary>Position of a set of points.</summary>
public enum PointPosition
{
    Normal,
    Reverse,
}

/// <summary>Kind of signal. Main signals start main routes; shunt signals start shunt routes.</summary>
public enum SignalType
{
    Main,
    Shunt,
}

/// <summary>
/// A track section (track circuit or axle-counter section) between two nodes.
/// A node is either a joint between sections, a line end, or a set of points (named by the points ID).
/// </summary>
public sealed record TrackSection(string Id, string EndA, string EndB, double LengthM)
{
    public bool Touches(string nodeId) => EndA == nodeId || EndB == nodeId;

    public string OtherEnd(string nodeId) =>
        nodeId == EndA ? EndB
        : nodeId == EndB ? EndA
        : throw new ArgumentException($"Section {Id} does not touch node {nodeId}.", nameof(nodeId));
}

/// <summary>
/// A set of points. Its ID is also the node ID that its three sections connect to.
/// A train arriving on the toe can go Normal or Reverse; a train arriving on a heel must leave by the toe.
/// <paramref name="LinkedWith"/> names the other end of a crossover, which is always worked in the same position.
/// </summary>
public sealed record Points(string Id, string ToeSection, string NormalSection, string ReverseSection, string? LinkedWith = null)
{
    public PointPosition? PositionFor(string heelSection) =>
        heelSection == NormalSection ? PointPosition.Normal
        : heelSection == ReverseSection ? PointPosition.Reverse
        : null;
}

/// <summary>
/// A signal standing at a node and reading into the section it protects.
/// The direction of the signal is "from AtNode into Protects".
/// </summary>
public sealed record Signal(string Id, SignalType Type, string AtNode, string Protects);

/// <summary>A point on the schematic drawing, in drawing units (not metres).</summary>
public sealed record DiagramPoint(double X, double Y);

/// <summary>A complete track layout: the input to every check.</summary>
/// <param name="Diagram">
/// Optional schematic coordinates: for each section, a line from its EndA to its EndB.
/// Only used for drawing; the checks never depend on it.
/// </param>
public sealed record Layout(
    string Name,
    IReadOnlyList<TrackSection> Sections,
    IReadOnlyList<Points> Points,
    IReadOnlyList<Signal> Signals,
    IReadOnlyDictionary<string, IReadOnlyList<DiagramPoint>>? Diagram = null);
