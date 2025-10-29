using WhatIsInMyFridge.Api.Models;

namespace WhatIsInMyFridge.Api.Services;

public sealed class ApplicationContext
{
    public User User { get; set; } = null!;
    public Household Household { get; set; } = null!;
    public bool IsAuthenticated => User != null && Household != null;
}
