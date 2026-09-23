using System.Runtime.CompilerServices;

// Allow the library test project to inspect internal helpers — currently
// FileDrop.ParseExtensions / HasAcceptedExtension for the behaviour-level
// unit tests.  Internal (not public) because these are implementation
// details of the attached behaviour: callers attach the behaviour via
// AXAML and don't reach into the helpers directly.
//
// ⚠ This must name the test ASSEMBLY that exists, and a wrong name fails silently: the grant simply
// goes nowhere, and only the test that needs it notices. It named `LayeredEditors.Avalonia.Tests`
// before the move and a non-existent `ScopedEditors.Avalonia.Tests` after it. This repository has
// one test project.
[assembly: InternalsVisibleTo("ScopedEditors.Tests")]