// monaco-interop.js — Bridge between Blazor (C#) and Monaco Editor
// Supports XML, JSON, YAML and JavaScript with full IntelliSense.

window.monacoInterop = (function () {
    'use strict';

    const _editors = {};
    const _dirty   = {};          // dirty-state por editor
    const MONACO_CDN = 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.0/min';

    // Providers são registrados globalmente no objeto monaco — só pode ser feito UMA vez.
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

        // ── XML: Variáveis Nativas Apigee (IntelliSense) ──────────────────────
        monaco.languages.registerCompletionItemProvider('xml', {
            triggerCharacters: ['.', '{', '>'],
            provideCompletionItems(model, position) {
                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endColumn: word.endColumn,
                };
                
                const apigeeVars = [
                    'request.header.', 'request.queryparam.', 'request.content', 'request.verb', 'request.path', 
                    'response.header.', 'response.content', 'response.status.code',
                    'proxy.pathsuffix', 'proxy.basepath', 'proxy.url',
                    'system.time', 'system.timestamp', 'system.uuid',
                    'target.host', 'target.port', 'target.url', 'target.ip'
                ];

                const suggestions = apigeeVars.map(v => ({
                    label: v,
                    kind: monaco.languages.CompletionItemKind.Variable,
                    insertText: v,
                    documentation: 'Apigee built-in flow variable',
                    range: range
                }));

                return { suggestions: suggestions };
            }
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

        // ── XML: DocumentSymbolProvider (Outline) ─────────────────────────────
        monaco.languages.registerDocumentSymbolProvider('xml', {
            provideDocumentSymbols(model, token) {
                const symbols = [];
                const regex = /<([a-zA-Z0-9_\-]+)([^>]*)>/g;
                let match;
                const text = model.getValue();
                
                while ((match = regex.exec(text)) !== null) {
                    // Ignora tags de fechamento se regex der match indesejado (mas o regex acima só pega aberturas/singles)
                    if (match[1].startsWith('/')) continue;

                    const tagName = match[1];
                    const attrs = match[2];
                    
                    const nameMatch = attrs.match(/name=["']([^"']+)["']/i);
                    const isStructureNode = ['ProxyEndpoint', 'TargetEndpoint', 'Flow', 'Step', 'FaultRule', 'APIProxy', 'SharedFlow'].includes(tagName);

                    if (nameMatch || isStructureNode) {
                        const name = nameMatch ? nameMatch[1] : tagName;
                        const detail = nameMatch ? tagName : "Apigee Node";
                        const kind = isStructureNode ? monaco.languages.SymbolKind.Struct : monaco.languages.SymbolKind.Object;

                        const position = model.getPositionAt(match.index);
                        // Tentamos achar a tag de fechamento para ter um range melhor (simplificado aqui)
                        const endPosition = model.getPositionAt(match.index + match[0].length);
                        
                        symbols.push({
                            name: name,
                            detail: detail,
                            kind: kind,
                            range: new monaco.Range(position.lineNumber, position.column, endPosition.lineNumber, endPosition.column),
                            selectionRange: new monaco.Range(position.lineNumber, position.column, endPosition.lineNumber, endPosition.column),
                            tags: []
                        });
                    }
                }
                return symbols;
            }
        });

        // ── Native Drag & Drop Formatter ──────────────────────────────────────
        // Intercepta de forma nativa o que for arrastado pelo Blazor para formatar automaticamente.
        if (monaco.languages.registerDocumentDropEditProvider) {
            monaco.languages.registerDocumentDropEditProvider('*', {
                provideDocumentDropEdits(model, position, dataTransfer, token) {
                    const item = dataTransfer.get('application/vnd.apigee.item');
                    if (item) {
                        const snippetStr = typeof item.asString === 'function' ? item.asString() : item.value;
                        return Promise.resolve(snippetStr).then(snippet => {
                            if (!snippet) return null;
                            return {
                                insertText: snippet,
                                insertTextFormat: monaco.languages.InsertTextFormat.Snippet,
                                handledMimeType: 'application/vnd.apigee.item'
                            };
                        });
                    }
                    return null;
                }
            });
        }
    }

    // Cria o editor Monaco no container assim que ele tiver dimensões reais.
    //
    // Estratégia:
    // 1. Se o container já tem altura (caso comum na primeira carga), cria imediatamente.
    // 2. Se não tem (retorno via navegação Blazor SPA), usa ResizeObserver — ele dispara
    //    exatamente quando o browser termina de aplicar o CSS e o container tem área real.
    //    Isso é garantido e não depende de contar frames arbitrários.
    function _createWhenVisible(container, factory) {
        if (container.clientHeight > 0) {
            factory();
            return;
        }

        const ro = new ResizeObserver(function (entries) {
            for (const entry of entries) {
                const h = entry.contentRect
                    ? entry.contentRect.height
                    : (entry.borderBoxSize && entry.borderBoxSize[0]
                        ? entry.borderBoxSize[0].blockSize
                        : 0);
                if (h > 0) {
                    ro.disconnect();
                    factory();
                    return;
                }
            }
        });
        ro.observe(container);

        // Timeout de segurança: se por algum motivo o ResizeObserver nunca disparar
        // (ex: container oculto permanentemente), não trava a página.
        setTimeout(function () {
            ro.disconnect();
        }, 5000);
    }

    // Resolve o objeto monaco via AMD require.
    // SEMPRE usa require() — mesmo quando window.monaco já existe.
    // O loader AMD mantém cache interno e retorna instantaneamente, mas
    // esse caminho garante que os web workers de tokenização estejam
    // conectados corretamente ao módulo (na navegação SPA do Blazor,
    // a referência em window.monaco pode ficar «stale» para os workers).
    function _withMonaco(cb) {
        _configureLoader();
        require(['vs/editor/editor.main'], function (monaco) {
            window.monaco = monaco;
            cb(monaco);
        });
    }

    return {
        create(elementId, initialContent, filePath) {
            try {
                // Dispor editor anterior se existir
                if (_editors[elementId]) {
                    _editors[elementId].dispose();
                    delete _editors[elementId];
                }
                delete _dirty[elementId];

                const container = document.getElementById(elementId);
                if (!container) return;

                const language = _detectLanguage(filePath);

                _withMonaco(function (monaco) {
                    _registerProviders(monaco);

                    // Limpar modelos órfãos de navegações anteriores
                    // para evitar leaks e conflitos de tokenização.
                    monaco.editor.getModels().forEach(function (m) { m.dispose(); });

                    // ── FIX CRÍTICO: Re-injetar CSS do tema ────────────────
                    // A navegação SPA do Blazor (enhanced navigation) pode
                    // remover os <style> que Monaco injeta dinamicamente no
                    // <head> para colorização de tokens. Chamar defineTheme()
                    // ANTES de criar o editor força Monaco a regenerar e
                    // re-injetar todas as regras CSS do tema no DOM.
                    monaco.editor.defineTheme('apigee-dark', {
                        base: 'vs-dark',
                        inherit: true,
                        rules: [],
                        colors: {}
                    });

                    _createWhenVisible(container, function () {
                        // Verifica novamente: o Blazor pode ter removido o container
                        // entre o momento em que agendamos e o ResizeObserver disparar.
                        if (!document.getElementById(elementId)) return;

                        // Criar modelo explicitamente para garantir que a linguagem
                        // e o tokenizer sejam inicializados antes do editor renderizar.
                        const model = monaco.editor.createModel(initialContent || '', language);

                        const editor = monaco.editor.create(container, {
                            model:             model,
                            theme:             'apigee-dark',
                            automaticLayout:   true,
                            dragAndDrop:       true,
                            dropIntoEditor:    { enabled: true },
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

                        // Forçar aplicação do tema e linguagem após criação
                        monaco.editor.setModelLanguage(model, language);
                        monaco.editor.setTheme('apigee-dark');

                        // ── Ctrl+S → save ──────────────────────────────────────
                        editor.addCommand(
                            monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS,
                            function () {
                                container.dispatchEvent(new CustomEvent('monaco-save', { bubbles: true }));
                            }
                        );

                        // ── Dirty state tracking ───────────────────────────────
                        _dirty[elementId] = false;
                        editor.onDidChangeModelContent(function () {
                            _dirty[elementId] = true;
                        });

                        _editors[elementId] = editor;
                    });
                });
            } catch (e) {
                console.error('[monacoInterop] create failed:', e);
            }
        },

        getValue(elementId) {
            try {
                const ed = _editors[elementId];
                return ed ? ed.getValue() : '';
            } catch (e) {
                console.error('[monacoInterop] getValue failed:', e);
                return '';
            }
        },

        setValue(elementId, content, filePath) {
            try {
                const ed = _editors[elementId];
                if (!ed) return;
                const language = _detectLanguage(filePath);
                _withMonaco(function (monaco) {
                    const model = ed.getModel();
                    if (model) monaco.editor.setModelLanguage(model, language);
                    ed.setValue(content || '');
                    ed.revealLine(1);

                    // Re-injetar tema ao trocar de arquivo
                    monaco.editor.defineTheme('apigee-dark', {
                        base: 'vs-dark', inherit: true, rules: [], colors: {}
                    });
                    monaco.editor.setTheme('apigee-dark');

                    // Resetar dirty state ao definir novo conteúdo
                    _dirty[elementId] = false;
                });
            } catch (e) {
                console.error('[monacoInterop] setValue failed:', e);
            }
        },

        dispose(elementId) {
            try {
                const ed = _editors[elementId];
                if (ed) {
                    ed.dispose();
                    delete _editors[elementId];
                }
                delete _dirty[elementId];
            } catch (e) {
                console.error('[monacoInterop] dispose failed:', e);
            }
        },

        formatDocument(elementId) {
            try {
                const ed = _editors[elementId];
                if (ed) ed.getAction('editor.action.formatDocument').run();
            } catch (e) {
                console.error('[monacoInterop] formatDocument failed:', e);
            }
        },

        isDirty(elementId) {
            return !!_dirty[elementId];
        },

        clearDirty(elementId) {
            _dirty[elementId] = false;
        },

        setMarkers(elementId, markersData) {
            try {
                const ed = _editors[elementId];
                if (!ed) return;
                
                _withMonaco(function (monaco) {
                    const model = ed.getModel();
                    if (!model) return;

                    const monacoMarkers = markersData.map(function (m) {
                        return {
                            startLineNumber: m.line,
                            endLineNumber: m.line,
                            startColumn: m.column,
                            endColumn: m.column + 5, // aprox
                            message: m.message,
                            severity: m.severity === 1 ? monaco.MarkerSeverity.Warning : monaco.MarkerSeverity.Error
                        };
                    });

                    monaco.editor.setModelMarkers(model, "apigeelint", monacoMarkers);
                });
            } catch (e) {
                console.error('[monacoInterop] setMarkers failed:', e);
            }
        }
    };
})();
