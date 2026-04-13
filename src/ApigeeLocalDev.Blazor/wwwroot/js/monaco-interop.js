// monaco-interop.js — Bridge between Blazor (C#) and Monaco Editor
// Supports XML, JSON, YAML and JavaScript with full IntelliSense.

window.monacoInterop = (function () {
    'use strict';

    const _editors = {};
    const MONACO_CDN = 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.0/min';

    // Providers são registrados globalmente no objeto monaco — só pode ser feito UMA vez.
    // Se registrar mais de uma vez o Monaco empilha múltiplos providers e o IntelliSense
    // para de funcionar após navegação (retorno à página).
    let _providersRegistered = false;

    function _configureLoader() {
        require.config({ paths: { vs: MONACO_CDN + '/vs' } });
    }

    function _detectLanguage(filePath) {
        if (!filePath) return 'plaintext';
        const ext = filePath.split('.').pop().toLowerCase();
        switch (ext) {
            case 'xml':  return 'xml';
            case 'json': return 'json';
            case 'js':   return 'javascript';
            case 'yaml':
            case 'yml':  return 'yaml';
            case 'md':   return 'markdown';
            case 'css':  return 'css';
            case 'html': return 'html';
            default:     return 'plaintext';
        }
    }

    function _registerProviders(monaco) {
        if (_providersRegistered) return;
        _providersRegistered = true;

        // ── XML: snippets Apigee ──────────────────────────────────────────────
        monaco.languages.registerCompletionItemProvider('xml', {
            triggerCharacters: ['<'],
            provideCompletionItems(model, position) {
                const word  = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber:   position.lineNumber,
                    startColumn:     word.startColumn,
                    endColumn:       word.endColumn,
                };
                return {
                    suggestions: [
                        {
                            label: 'ProxyEndpoint',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<ProxyEndpoint name="${1:default}">\n' +
                                '    <HTTPProxyConnection>\n' +
                                '        <BasePath>/${2:base-path}</BasePath>\n' +
                                '        <VirtualHost>default</VirtualHost>\n' +
                                '    </HTTPProxyConnection>\n' +
                                '    <PreFlow name="PreFlow">\n' +
                                '        <Request/>\n' +
                                '        <Response/>\n' +
                                '    </PreFlow>\n' +
                                '    <PostFlow name="PostFlow">\n' +
                                '        <Request/>\n' +
                                '        <Response/>\n' +
                                '    </PostFlow>\n' +
                                '    <Flows/>\n' +
                                '    <RouteRule name="default">\n' +
                                '        <TargetEndpoint>default</TargetEndpoint>\n' +
                                '    </RouteRule>\n' +
                                '</ProxyEndpoint>',
                            documentation: 'Apigee ProxyEndpoint scaffold',
                            range,
                        },
                        {
                            label: 'TargetEndpoint',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<TargetEndpoint name="${1:default}">\n' +
                                '    <PreFlow name="PreFlow">\n' +
                                '        <Request/>\n' +
                                '        <Response/>\n' +
                                '    </PreFlow>\n' +
                                '    <PostFlow name="PostFlow">\n' +
                                '        <Request/>\n' +
                                '        <Response/>\n' +
                                '    </PostFlow>\n' +
                                '    <Flows/>\n' +
                                '    <HTTPTargetConnection>\n' +
                                '        <URL>${2:https://api.example.com}</URL>\n' +
                                '    </HTTPTargetConnection>\n' +
                                '</TargetEndpoint>',
                            documentation: 'Apigee TargetEndpoint scaffold',
                            range,
                        },
                        {
                            label: 'AssignMessage',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<AssignMessage name="${1:AM-PolicyName}">\n' +
                                '    <AssignTo createNew="false" type="request"/>\n' +
                                '    <Set>\n' +
                                '        <Headers>\n' +
                                '            <Header name="${2:X-Custom-Header}">${3:value}</Header>\n' +
                                '        </Headers>\n' +
                                '    </Set>\n' +
                                '    <IgnoreUnresolvedVariables>false</IgnoreUnresolvedVariables>\n' +
                                '</AssignMessage>',
                            documentation: 'Apigee AssignMessage policy',
                            range,
                        },
                        {
                            label: 'VerifyAPIKey',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<VerifyAPIKey name="${1:VAK-PolicyName}">\n' +
                                '    <APIKey ref="${2:request.queryparam.apikey}"/>\n' +
                                '</VerifyAPIKey>',
                            documentation: 'Apigee VerifyAPIKey policy',
                            range,
                        },
                        {
                            label: 'SpikeArrest',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<SpikeArrest name="${1:SA-PolicyName}">\n' +
                                '    <Rate>${2:30ps}</Rate>\n' +
                                '    <UseEffectiveCount>true</UseEffectiveCount>\n' +
                                '</SpikeArrest>',
                            documentation: 'Apigee SpikeArrest policy',
                            range,
                        },
                        {
                            label: 'OAuthV2-VerifyToken',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<OAuthV2 name="${1:OA-VerifyToken}">\n' +
                                '    <Operation>VerifyAccessToken</Operation>\n' +
                                '</OAuthV2>',
                            documentation: 'Apigee OAuthV2 VerifyAccessToken',
                            range,
                        },
                        {
                            label: 'OAuthV2-GenerateAccessToken',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<OAuthV2 name="${1:OA-GenerateToken}">\n' +
                                '    <Operation>GenerateAccessToken</Operation>\n' +
                                '    <ExpiresIn>${2:3600000}</ExpiresIn>\n' +
                                '    <SupportedGrantTypes>\n' +
                                '        <GrantType>client_credentials</GrantType>\n' +
                                '    </SupportedGrantTypes>\n' +
                                '    <GenerateResponse enabled="true"/>\n' +
                                '</OAuthV2>',
                            documentation: 'Apigee OAuthV2 GenerateAccessToken',
                            range,
                        },
                        {
                            label: 'Quota',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<Quota name="${1:Q-PolicyName}">\n' +
                                '    <Allow count="${2:1000}"/>\n' +
                                '    <Interval>1</Interval>\n' +
                                '    <TimeUnit>hour</TimeUnit>\n' +
                                '    <Identifier ref="request.queryparam.apikey"/>\n' +
                                '    <Distributed>false</Distributed>\n' +
                                '    <Synchronous>false</Synchronous>\n' +
                                '</Quota>',
                            documentation: 'Apigee Quota policy',
                            range,
                        },
                        {
                            label: 'RaiseFault',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<RaiseFault name="${1:RF-PolicyName}">\n' +
                                '    <FaultResponse>\n' +
                                '        <Set>\n' +
                                '            <StatusCode>${2:400}</StatusCode>\n' +
                                '            <ReasonPhrase>${3:Bad Request}</ReasonPhrase>\n' +
                                '            <Payload contentType="application/json">{"error":"${3:Bad Request}"}</Payload>\n' +
                                '        </Set>\n' +
                                '    </FaultResponse>\n' +
                                '    <IgnoreUnresolvedVariables>true</IgnoreUnresolvedVariables>\n' +
                                '</RaiseFault>',
                            documentation: 'Apigee RaiseFault policy',
                            range,
                        },
                        {
                            label: 'ExtractVariables',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<ExtractVariables name="${1:EV-PolicyName}">\n' +
                                '    <Source>request</Source>\n' +
                                '    <QueryParam name="${2:paramName}">\n' +
                                '        <Pattern ignoreCase="true">${3:{pattern}}</Pattern>\n' +
                                '    </QueryParam>\n' +
                                '    <VariablePrefix>${4:prefix}</VariablePrefix>\n' +
                                '    <IgnoreUnresolvedVariables>true</IgnoreUnresolvedVariables>\n' +
                                '</ExtractVariables>',
                            documentation: 'Apigee ExtractVariables policy',
                            range,
                        },
                        {
                            label: 'ServiceCallout',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<ServiceCallout name="${1:SC-PolicyName}">\n' +
                                '    <Request variable="${2:myRequest}"/>\n' +
                                '    <Response>${3:myResponse}</Response>\n' +
                                '    <HTTPTargetConnection>\n' +
                                '        <URL>${4:https://api.example.com/endpoint}</URL>\n' +
                                '    </HTTPTargetConnection>\n' +
                                '</ServiceCallout>',
                            documentation: 'Apigee ServiceCallout policy',
                            range,
                        },
                        {
                            label: 'ResponseCache',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<ResponseCache name="${1:RC-PolicyName}">\n' +
                                '    <CacheKey>\n' +
                                '        <KeyFragment ref="request.uri" type="string"/>\n' +
                                '    </CacheKey>\n' +
                                '    <ExpirySettings>\n' +
                                '        <TimeoutInSeconds>${2:300}</TimeoutInSeconds>\n' +
                                '    </ExpirySettings>\n' +
                                '    <SkipCacheLookup>false</SkipCacheLookup>\n' +
                                '    <SkipCachePopulation>false</SkipCachePopulation>\n' +
                                '</ResponseCache>',
                            documentation: 'Apigee ResponseCache policy',
                            range,
                        },
                        {
                            label: 'KeyValueMapOperations',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<KeyValueMapOperations name="${1:KVM-PolicyName}" mapIdentifier="${2:myMap}">\n' +
                                '    <Scope>environment</Scope>\n' +
                                '    <Get assignTo="${3:var.myValue}" index="1">\n' +
                                '        <Key>\n' +
                                '            <Parameter>${4:myKey}</Parameter>\n' +
                                '        </Key>\n' +
                                '    </Get>\n' +
                                '</KeyValueMapOperations>',
                            documentation: 'Apigee KeyValueMapOperations policy',
                            range,
                        },
                        {
                            label: 'Javascript',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<Javascript name="${1:JS-PolicyName}" timeLimit="200">\n' +
                                '    <ResourceURL>jsc://${2:script.js}</ResourceURL>\n' +
                                '</Javascript>',
                            documentation: 'Apigee JavaScript policy',
                            range,
                        },
                        {
                            label: 'MessageLogging',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<MessageLogging name="${1:ML-PolicyName}">\n' +
                                '    <Syslog>\n' +
                                '        <Message>${2:{message}}</Message>\n' +
                                '        <Host>${3:logs.example.com}</Host>\n' +
                                '        <Port>${4:514}</Port>\n' +
                                '        <Protocol>UDP</Protocol>\n' +
                                '        <FormatMessage>true</FormatMessage>\n' +
                                '    </Syslog>\n' +
                                '    <logLevel>INFO</logLevel>\n' +
                                '</MessageLogging>',
                            documentation: 'Apigee MessageLogging policy',
                            range,
                        },
                        {
                            label: 'Flow',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<Flow name="${1:FlowName}">\n' +
                                '    <Description>${2:Flow description}</Description>\n' +
                                '    <Request>\n' +
                                '        <Step>\n' +
                                '            <Name>${3:PolicyName}</Name>\n' +
                                '        </Step>\n' +
                                '    </Request>\n' +
                                '    <Response/>\n' +
                                '    <Condition>${4:(proxy.pathsuffix MatchesPath "/path")}</Condition>\n' +
                                '</Flow>',
                            documentation: 'Apigee conditional Flow',
                            range,
                        },
                        {
                            label: 'Step',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '<Step>\n' +
                                '    <Name>${1:PolicyName}</Name>\n' +
                                '    <Condition>${2:request.verb = "GET"}</Condition>\n' +
                                '</Step>',
                            documentation: 'Apigee policy Step with optional Condition',
                            range,
                        },
                    ],
                };
            },
        });

        // ── JSON: snippets Apigee ─────────────────────────────────────────────
        monaco.languages.registerCompletionItemProvider('json', {
            triggerCharacters: ['"', '{'],
            provideCompletionItems(model, position) {
                const word  = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber:   position.lineNumber,
                    startColumn:     word.startColumn,
                    endColumn:       word.endColumn,
                };
                return {
                    suggestions: [
                        {
                            label: 'deployments.json scaffold',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText: '{\n  "proxies": ["${1:proxy-name}"],\n  "sharedFlows": []\n}',
                            documentation: 'Apigee deployments.json',
                            range,
                        },
                        {
                            label: 'flowhooks.json scaffold',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '{\n' +
                                '  "PreProxyFlowHook":   {},\n' +
                                '  "PostProxyFlowHook":  {},\n' +
                                '  "PreTargetFlowHook":  {},\n' +
                                '  "PostTargetFlowHook": {}\n' +
                                '}',
                            documentation: 'Apigee flowhooks.json',
                            range,
                        },
                        {
                            label: 'targetserver entry',
                            kind:  monaco.languages.CompletionItemKind.Snippet,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            insertText:
                                '{\n' +
                                '  "name":    "${1:myTargetServer}",\n' +
                                '  "host":    "${2:api.example.com}",\n' +
                                '  "port":    ${3:443},\n' +
                                '  "isEnabled": true,\n' +
                                '  "sSLInfo": { "enabled": true }\n' +
                                '}',
                            documentation: 'Apigee TargetServer entry',
                            range,
                        },
                    ],
                };
            },
        });
    }

    // Aguarda o container ter clientHeight > 0 antes de criar o editor.
    function _waitForHeight(container, cb, attempts) {
        attempts = attempts || 0;
        if (container.clientHeight > 0 || attempts >= 20) {
            cb();
        } else {
            requestAnimationFrame(function () {
                _waitForHeight(container, cb, attempts + 1);
            });
        }
    }

    // Resolve o objeto monaco: usa window.monaco se já carregado (navegação de volta
    // à página), ou carrega via AMD. Isso evita que o callback do require() não seja
    // chamado quando o módulo já está em cache — comportamento do AMD loader.
    function _withMonaco(cb) {
        if (window.monaco) {
            cb(window.monaco);
        } else {
            _configureLoader();
            require(['vs/editor/editor.main'], function (monaco) {
                // Expõe globalmente para reuso em navegações futuras
                window.monaco = monaco;
                cb(monaco);
            });
        }
    }

    return {
        create(elementId, initialContent, filePath) {
            if (_editors[elementId]) {
                _editors[elementId].dispose();
                delete _editors[elementId];
            }

            const container = document.getElementById(elementId);
            if (!container) return;

            const language = _detectLanguage(filePath);

            _withMonaco(function (monaco) {
                // Registra providers uma única vez para toda a sessão
                _registerProviders(monaco);

                _waitForHeight(container, function () {
                    const editor = monaco.editor.create(container, {
                        value:             initialContent || '',
                        language:          language,
                        theme:             'vs-dark',
                        automaticLayout:   true,
                        fontSize:          13,
                        fontFamily:        '"JetBrains Mono", "Fira Code", monospace',
                        fontLigatures:     true,
                        minimap:           { enabled: false },
                        scrollBeyondLastLine: false,
                        wordWrap:          'off',
                        tabSize:           4,
                        insertSpaces:      true,
                        formatOnType:      true,
                        formatOnPaste:     true,
                        suggestOnTriggerCharacters: true,
                        quickSuggestions:  { other: true, comments: false, strings: true },
                        acceptSuggestionOnEnter: 'on',
                        renderLineHighlight: 'line',
                        bracketPairColorization: { enabled: true },
                        guides: { bracketPairs: true, indentation: true },
                    });

                    editor.addCommand(
                        monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS,
                        function () {
                            container.dispatchEvent(new CustomEvent('monaco-save', { bubbles: true }));
                        }
                    );

                    _editors[elementId] = editor;
                });
            });
        },

        getValue(elementId) {
            const ed = _editors[elementId];
            return ed ? ed.getValue() : '';
        },

        setValue(elementId, content, filePath) {
            const ed = _editors[elementId];
            if (!ed) return;
            const language = _detectLanguage(filePath);
            _withMonaco(function (monaco) {
                const model = ed.getModel();
                if (model) monaco.editor.setModelLanguage(model, language);
                ed.setValue(content || '');
                ed.revealLine(1);
            });
        },

        dispose(elementId) {
            const ed = _editors[elementId];
            if (ed) {
                ed.dispose();
                delete _editors[elementId];
            }
        },

        formatDocument(elementId) {
            const ed = _editors[elementId];
            if (ed) ed.getAction('editor.action.formatDocument').run();
        },
    };
})();
