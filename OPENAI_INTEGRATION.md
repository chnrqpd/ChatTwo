# Integração OpenAI - Resumo Técnico

## O que foi implementado

Adicionado suporte para tradução direta usando a API da OpenAI, sem necessidade de plugins externos.

## Arquivos Modificados

### 1. `Configuration.cs`
Adicionados novos campos:
- `UseOpenAIDirectly` (bool) - Habilita tradução direta via OpenAI
- `OpenAIApiKey` (string) - Chave da API OpenAI
- `OpenAIModel` (string) - Modelo a usar (padrão: gpt-4o-mini)
- `OpenAIBaseUrl` (string) - URL da API (padrão: https://api.openai.com/v1/chat/completions)

### 2. `TranslationBridge.cs`
- Adicionado `HttpClient` estático para requisições HTTP
- Novo método `TranslateViaOpenAI()` que:
  - Cria um prompt de tradução
  - Envia requisição POST para a API OpenAI
  - Parseia o JSON de resposta
  - Retorna o texto traduzido
- Modificado `Translate()` para priorizar OpenAI quando `UseOpenAIDirectly` está habilitado
- Fallback para IPC quando OpenAI não está configurado

### 3. `SettingsTabs/Miscellaneous.cs`
- Interface de configuração atualizada com:
  - Toggle "Use OpenAI API directly"
  - Campo de API Key (com password masking)
  - Campo de seleção de modelo
  - Campo de URL customizada
  - Campos condicionalmente visíveis (OpenAI vs IPC)

### 4. `MessageManager.cs` e logs adicionais
- Adicionados logs de debug para rastreamento

## Como Funciona

### Fluxo de Tradução (OpenAI Direto)

```
1. Mensagem recebida
   ↓
2. TranslateIncoming() chamado
   ↓
3. Verifica se UseOpenAIDirectly = true
   ↓
4. TranslateViaOpenAI() é executado assincronamente
   ↓
5. Requisição HTTP para OpenAI API
   ↓
6. Resposta parseada
   ↓
7. message.SetTranslation() armazena resultado
   ↓
8. UI renderiza tradução abaixo da mensagem
```

### Formato da Requisição OpenAI

```json
{
  "model": "gpt-4o-mini",
  "messages": [
    {
      "role": "system",
      "content": "You are a translator. Translate text accurately while preserving the original meaning and tone."
    },
    {
      "role": "user",
      "content": "Translate the following text from auto to pt-BR. Only respond with the translation, nothing else:\n\nHello world"
    }
  ],
  "temperature": 0.3,
  "max_tokens": 500
}
```

### Formato da Resposta

```json
{
  "choices": [
    {
      "message": {
        "content": "Olá mundo"
      }
    }
  ]
}
```

## Tratamento de Erros

- **API Key inválida**: Log de warning + retorna null
- **Rate limit excedido**: Log de warning com código HTTP + retorna null  
- **Timeout/Conexão**: Exception capturada + log de erro + retorna null
- **Resposta JSON inválida**: Exception capturada + retorna null

Em todos os casos de erro, a mensagem original é exibida sem tradução.

## Segurança

- API Key é armazenada em plaintext no config (padrão do Dalamud)
- Campo de senha mascarado na UI (ImGuiInputTextFlags.Password)
- Comunicação via HTTPS com OpenAI
- Nenhum dado enviado para terceiros além da OpenAI

## Performance

- Tradução assíncrona (não bloqueia UI)
- HttpClient estático reutilizado (pool de conexões)
- Timeout configurável (padrão 2s para outgoing)
- Max tokens limitado a 500 (controle de custos)

## Custos Estimados (gpt-4o-mini)

- Input: $0.15 / 1M tokens
- Output: $0.60 / 1M tokens
- Mensagem típica: ~50 tokens in + ~50 tokens out = ~$0.00004 USD/mensagem
- 1000 mensagens ≈ $0.04 USD

## Compatibilidade

- Mantém compatibilidade total com sistema IPC existente
- Se `UseOpenAIDirectly = false`, usa IPC como antes
- Sem breaking changes na API pública
- Configuração salva no arquivo JSON do plugin

## Logs de Debug

Quando ativados, mostram:
- `Using OpenAI direct translation` - Modo OpenAI ativo
- `Sending OpenAI request for text: '...'` - Requisição enviada
- `OpenAI translation: 'X' -> 'Y'` - Tradução bem-sucedida
- `OpenAI API error: XXX - ...` - Erro HTTP da API
- `Failed to translate via OpenAI` - Exception genérica

## Próximas Melhorias Possíveis

1. Cache de traduções (evitar retraduzir texto idêntico)
2. Configuração de timeout customizável
3. Suporte para outros provedores (Anthropic, Azure OpenAI, etc)
4. Rate limiting local (evitar custos excessivos)
5. Estatísticas de uso e custos
6. Batch translation (traduzir múltiplas mensagens numa requisição)
