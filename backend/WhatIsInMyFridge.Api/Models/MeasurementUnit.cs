namespace WhatIsInMyFridge.Api.Models;

public enum MeasurementUnit
{
    // Count
    Pieces,
    Items,
    
    // Weight (Metric)
    Grams,
    Kilograms,
    
    // Weight (Imperial)
    Ounces,
    Pounds,
    
    // Volume (Metric)
    Milliliters,
    Liters,
    
    // Volume (Imperial)
    FluidOunces,
    Cups,
    Pints,
    Quarts,
    Gallons,
    
    // Cooking measurements
    Teaspoons,
    Tablespoons,
    
    // Packaging
    Cans,
    Bottles,
    Jars,
    Boxes,
    Bags,
    Packages,
    Cartons,
    
    Undefined
}
