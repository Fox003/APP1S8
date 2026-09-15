namespace SONDAGEAPI.DTOs;

public record SurveySummaryDTO(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt
);