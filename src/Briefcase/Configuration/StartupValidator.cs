namespace Briefcase.Configuration;

public static class StartupValidator
{
    public static bool Validate(AppSettings settings, string? envFileLoaded)
    {
        var valid = true;

        if (envFileLoaded is not null)
            Console.Error.WriteLine($"[Briefcase] Loaded .env from: {envFileLoaded}");
        else
            Console.Error.WriteLine("[Briefcase] No .env file found. Using system environment variables only.");

        if (string.IsNullOrEmpty(settings.DataPath))
        {
            Console.Error.WriteLine("[Briefcase] ERROR: BRIEFCASE_DATA_PATH is not set.");
            Console.Error.WriteLine("           This is required — it tells the server where to store the file registry.");
            Console.Error.WriteLine("           Example: BRIEFCASE_DATA_PATH=C:\\Users\\you\\.briefcase");
            valid = false;
        }

        if (settings.BriefcasePaths.Length == 0)
        {
            Console.Error.WriteLine("[Briefcase] WARNING: BRIEFCASE_PATHS is not set. No files will be visible to agents.");
            Console.Error.WriteLine("            Example: BRIEFCASE_PATHS=C:\\Users\\you\\Documents;D:\\notes");
        }
        else
        {
            foreach (var path in settings.BriefcasePaths)
            {
                if (!Directory.Exists(path))
                    Console.Error.WriteLine($"[Briefcase] WARNING: Configured path does not exist and will be skipped: {path}");
            }
        }

        if (!valid)
        {
            Console.Error.WriteLine("[Briefcase] Cannot start due to the configuration error(s) listed above.");
            Console.Error.WriteLine("            Add a .env file next to the executable or set system environment variables.");
        }

        return valid;
    }
}
