namespace OrderConverterEXE;

public class Configuration
{
    public string BaseDir { get; set; } = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
    public string SourceDir { get; set; } = Path.Combine("docs", "sourceData");
    public string TargetDir { get; set; } = Path.Combine("docs", "convertedData");
    public int CheckIntervalSeconds { get; set; } = 10;

    public string GetSourcePath() => Path.Combine(BaseDir, SourceDir);
    public string GetTargetPath() => Path.Combine(BaseDir, TargetDir);
    public string GetQuotationPath() => Path.Combine(BaseDir, TargetDir, "LG-Le", "Quotation");
}
