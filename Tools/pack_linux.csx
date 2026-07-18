#r "nuget: SimpleExec, 6.2.0"

using static SimpleExec.Command;

// Keep the historical entry point, but delegate to the canonical Linux builder.
// This avoids maintaining a second archive manifest that can omit the plugin or web assets.
if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "run", "build-linux-package.sh")))
	throw new FileNotFoundException("Run this script from the repository root.");

Run("sh", "run/build-linux-package.sh");
