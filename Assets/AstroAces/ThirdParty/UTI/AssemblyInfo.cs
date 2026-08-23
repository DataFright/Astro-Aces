using System.Runtime.CompilerServices;

// Lets UTI.Tests exercise internal seams (e.g. BeanVisualizer.IGizmoDrawer/DrawPath) directly,
// rather than only what's public. See BeanVisualizer.cs for why that seam exists.
[assembly: InternalsVisibleTo("UTI.Tests")]
