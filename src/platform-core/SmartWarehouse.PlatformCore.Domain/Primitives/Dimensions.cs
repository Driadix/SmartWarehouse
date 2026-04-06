namespace SmartWarehouse.PlatformCore.Domain.Primitives;

public readonly record struct Dimensions
{
  public Dimensions(decimal length, decimal width, decimal height)
  {
    Length = DomainGuard.Positive(length, nameof(length));
    Width = DomainGuard.Positive(width, nameof(width));
    Height = DomainGuard.Positive(height, nameof(height));
  }

  public decimal Length { get; }

  public decimal Width { get; }

  public decimal Height { get; }
}
