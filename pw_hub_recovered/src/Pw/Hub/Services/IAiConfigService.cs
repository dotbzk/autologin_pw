namespace Pw.Hub.Services;

public interface IAiConfigService
{
	AiConfig Load();

	void Save(AiConfig cfg);

	AiConfig GetEffective();
}
