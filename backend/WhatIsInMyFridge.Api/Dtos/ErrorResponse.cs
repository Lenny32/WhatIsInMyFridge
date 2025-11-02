namespace WhatIsInMyFridge.Api.Dtos;

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public ErrorResponse? InnerException { get; set; }
}
