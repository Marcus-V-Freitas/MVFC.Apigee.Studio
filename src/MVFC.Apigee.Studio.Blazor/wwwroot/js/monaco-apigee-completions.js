// monaco-apigee-completions.js — Contextual autocomplete for Apigee Policies
(function() {
    let _completions = null;

    async function loadCompletions() {
        try {
            const res = await fetch('/monaco/apigee-completions.json');
            _completions = await res.json();
            console.log('[ApigeeCompletions] Loaded successfully');
        } catch (e) {
            console.error('[ApigeeCompletions] Failed to load:', e);
        }
    }

    function getParentElement(model, position) {
        // Simple scanner to find the parent tag
        const textBefore = model.getValueInRange({
            startLineNumber: 1, startColumn: 1,
            endLineNumber: position.lineNumber, endColumn: position.column
        });
        
        // Match opening tags that are not self-closing and don't have a matching closing tag yet
        const openTags = [];
        const tagRegex = /<([a-zA-Z0-9\-:]+)(?:\s+[^>]*)?(\/?)>/g;
        let match;
        
        while ((match = tagRegex.exec(textBefore)) !== null) {
            const tagName = match[1];
            const isSelfClosing = match[2] === '/';
            
            if (!isSelfClosing) {
                openTags.push(tagName);
            }
        }
        
        // This is a naive implementation; ideally we should track closing tags to pop from the stack
        // But for most policy files (which are shallow), the last non-closed tag is usually the parent.
        return openTags.length > 0 ? openTags[openTags.length - 1] : null;
    }

    function getRootPolicyType(model) {
        const text = model.getValueInRange({ startLineNumber: 1, startColumn: 1, endLineNumber: 10, endColumn: 100 });
        const match = text.match(/<([a-zA-Z0-9\-:]+)\s/);
        return match ? match[1] : null;
    }

    function registerProvider(monaco) {
        monaco.languages.registerCompletionItemProvider('xml', {
            triggerCharacters: ['<', ' ', '"'],
            provideCompletionItems: function(model, position) {
                if (!_completions) return { suggestions: [] };

                const policyType = getRootPolicyType(model);
                if (!policyType || !_completions[policyType]) return { suggestions: [] };

                const wordInfo = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: wordInfo.startColumn,
                    endColumn: position.column
                };

                const lineText = model.getLineContent(position.lineNumber).substring(0, position.column);
                
                // Context: Inside a tag (after space) -> suggest attributes
                if (lineText.match(/<[a-zA-Z0-9\-:]+\s+[^>]*$/)) {
                    const currentTagMatch = lineText.match(/<([a-zA-Z0-9\-:]+)\s+/);
                    const currentTag = currentTagMatch ? currentTagMatch[1] : policyType;
                    
                    // We can look up attributes for the specific tag if we had them in JSON, 
                    // but for now we use the policy-level attributes.
                    const attrs = _completions[policyType].attributes ?? [];
                    
                    return {
                        suggestions: attrs.map(attr => ({
                            label: attr,
                            kind: monaco.languages.CompletionItemKind.Property,
                            insertText: `${attr}="$1"`,
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            range
                        }))
                    };
                }

                // Context: After '<' -> suggest child elements
                if (lineText.trim().endsWith('<')) {
                    const parent = getParentElement(model, position) || policyType;
                    const children = _completions[policyType].children?.[parent] ?? [];
                    
                    return {
                        suggestions: children.map(child => ({
                            label: child,
                            kind: monaco.languages.CompletionItemKind.Field,
                            insertText: `${child}>$1</${child}>`, // the '<' is already typed
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            range
                        }))
                    };
                }

                return { suggestions: [] };
            }
        });
    }

    // Wait for monaco to be available
    if (window.monaco) {
        registerProvider(window.monaco);
    } else {
        const interval = setInterval(() => {
            if (window.monaco) {
                registerProvider(window.monaco);
                clearInterval(interval);
            }
        }, 500);
    }

    loadCompletions();
})();
