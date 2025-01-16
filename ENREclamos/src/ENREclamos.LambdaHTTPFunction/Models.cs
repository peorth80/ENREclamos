namespace ENREclamo.LambdaHTTPFunction;

public record ScheduleInformation
{
	public string ARN { get; set; } = null!;
	public string Name { get; set; } = null!;
	public string Group { get; set; } = null!;
}

public record Status
{
	public bool Success { get; set; }
	public string Message { get; set; } = null!;
}