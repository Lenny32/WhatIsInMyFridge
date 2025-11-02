using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace WhatIsInMyFridge.Api.Infrastructure;

internal static class ValidationHelper
{
    public static bool TryValidate(object model, out Dictionary<string, string[]> errors)
    {
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(model, validationContext, validationResults, true);
        errors = validationResults
            .GroupBy(result => result.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(
                group => group.Key,
                group => group.Select(result => result.ErrorMessage ?? "Invalid value").ToArray());

        return isValid;
    }
}
