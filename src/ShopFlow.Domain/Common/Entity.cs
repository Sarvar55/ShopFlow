namespace ShopFlow.Domain.Common;

public abstract class Entity
{
    // Version 7 GUIDs are time-ordered, which keeps B-tree indexes compact.
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
