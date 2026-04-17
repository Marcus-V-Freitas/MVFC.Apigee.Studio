namespace MVFC.Apigee.Studio.Application.Services;

public sealed class SkeletonTemplateService
{
    public string GetSharedFlowBundleXml(string name) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <SharedFlowBundle name="{name}">
            <Description>{name}</Description>
            <Revision>1</Revision>
            <SharedFlows>
                <SharedFlow>default</SharedFlow>
            </SharedFlows>
        </SharedFlowBundle>
        """;

    public string GetSharedFlowXml(string name) =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <SharedFlow name="default">
            <Description>Default shared flow</Description>
        </SharedFlow>
        """;

    public string GetDeploymentsJson() =>
        """
        {
          "proxies": [],
          "sharedFlows": []
        }
        """;

    public string GetFlowhooksJson() => "{}";

    public string GetTargetServersJson() => "[]\n";

    public string GetApiProxyDescriptorXml(string name) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <APIProxy name="{name}">
            <Description>{name}</Description>
            <Revision>1</Revision>
        </APIProxy>
        """;

    public string GetProxyEndpointXml(string name) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <ProxyEndpoint name="default">
            <Description>{name} proxy endpoint</Description>
            <HTTPProxyConnection>
                <BasePath>/{name}</BasePath>
                <VirtualHost>default</VirtualHost>
            </HTTPProxyConnection>
            <PreFlow name="PreFlow">
                <Request/>
                <Response/>
            </PreFlow>
            <PostFlow name="PostFlow">
                <Request/>
                <Response/>
            </PostFlow>
            <Flows/>
            <RouteRule name="default">
                <TargetEndpoint>default</TargetEndpoint>
            </RouteRule>
        </ProxyEndpoint>
        """;

    public string GetTargetEndpointXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <TargetEndpoint name="default">
            <Description>Default target endpoint</Description>
            <PreFlow name="PreFlow">
                <Request/>
                <Response/>
            </PreFlow>
            <PostFlow name="PostFlow">
                <Request/>
                <Response/>
            </PostFlow>
            <Flows/>
            <HTTPTargetConnection>
                <URL>https://httpbin.org/anything</URL>
            </HTTPTargetConnection>
        </TargetEndpoint>
        """;
}
