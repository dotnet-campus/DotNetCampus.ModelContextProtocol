namespace DotNetCampus.ModelContextProtocol.Protocol;

public abstract record PaginatedResult : Result
{
    private protected PaginatedResult()
    {
    }

    public string? NextCursor { get; set; }
}