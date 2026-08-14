namespace GumrukTarifeOyunu;

public class CustomsTariff(string description, string code, string chapter, string details = "")
{
    public string Description { get; set; } = description;
    public string Code { get; set; } = code;
    public string Chapter { get; set; } = chapter;
    public string Details { get; set; } = details;
}