namespace IotWelt.Common;

public record PagedResult<T>(List<T> Items, int TotalCount);
