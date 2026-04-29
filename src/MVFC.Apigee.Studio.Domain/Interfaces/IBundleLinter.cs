using MVFC.Apigee.Studio.Domain.Entities;

namespace MVFC.Apigee.Studio.Domain.Interfaces;

public interface IBundleLinter
{
    /// <summary>
    /// Performs a structural lint of the bundle.
    /// </summary>
    LintResult Lint(string workspaceRoot, string proxyName);
}
