namespace FaraTaraz.Core.Application;

/// <summary>
/// Marker for an application use case — the authoritative interface to platform behavior.
///
/// REST, MCP, and background sync ALL delegate here. Nothing bypasses this boundary:
///   REST / MCP / SyncWorker → Application → Domain/Analytics → Persistence
///
/// Forbidden: Dashboard → Database, MCP → Database, LLM → Database.
/// W0: no concrete use cases yet.
/// </summary>
public interface IApplicationUseCase
{
}
