namespace CareerForge.Infrastructure;

public static class LocalDataPaths
{
    public static string DirectoryPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CareerForge");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string ProfilePath => Path.Combine(DirectoryPath, "profile.json");
    public static string AssessmentPath => Path.Combine(DirectoryPath, "assessments.json");
}
