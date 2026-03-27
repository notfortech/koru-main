using System.ComponentModel.DataAnnotations;

namespace StudioTechBI.Application.DTOs.AI;

public class InsightRequest
{
    [Required]
    [StringLength(32)]
    public string PeriodType { get; set; } = "";
    public string? Period { get; set; }
    public string? ClientCode { get; set; }
}
