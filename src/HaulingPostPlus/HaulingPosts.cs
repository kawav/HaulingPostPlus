using HaulingPostPlus.Core;
using Timberborn.TemplateSystem;
using Timberborn.WorkSystem;

namespace HaulingPostPlus;

internal static class HaulingPosts
{
    public static bool IsSupported(Workplace? workplace)
    {
        if (!workplace)
            return false;
        var template = workplace!.GetComponent<TemplateSpec>();
        return template != null && SupportedTemplates.Contains(template.TemplateName);
    }

}

internal sealed class WorkplaceCountAdapter : IWorkerCount
{
    private readonly Workplace _workplace;
    public WorkplaceCountAdapter(Workplace workplace) => _workplace = workplace;
    public int DesiredWorkers => _workplace.DesiredWorkers;
    public int MaxWorkers => _workplace.MaxWorkers;
    public void Increase() => _workplace.IncreaseDesiredWorkers();
    public void Decrease() => _workplace.DecreaseDesiredWorkers();
}
