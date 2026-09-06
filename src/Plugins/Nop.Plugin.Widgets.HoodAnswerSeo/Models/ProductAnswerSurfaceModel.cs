namespace Nop.Plugin.Widgets.HoodAnswerSeo.Models;

public sealed record ProductAnswerSurfaceModel(
    string Category,
    string Sku,
    string Availability,
    string Shipping,
    int ReviewCount,
    decimal AverageRating,
    AnswerSurfaceLabels Labels,
    string PageSchemaJson);

public sealed record AnswerSurfaceLabels(
    string ProductFacts,
    string WhatItIs,
    string Category,
    string Sku,
    string Availability,
    string Shipping,
    string CustomerRating,
    string WhatCustomersSay,
    string FreeShipping,
    string AnonymousCustomer);
