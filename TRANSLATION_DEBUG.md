# Tradução de Chat com OpenAI - Guia de Configuração

## ✅ Implementação Concluída

O ChatTwo agora suporta **tradução direta via API do OpenAI**! Você não precisa mais de um plugin externo.

## Como Configurar

### 1. Obter sua API Key da OpenAI

1. Acesse [platform.openai.com](https://platform.openai.com)
2. Faça login ou crie uma conta
3. Vá para **API Keys**
4. Clique em **Create new secret key**
5. Copie a chave (começa com `sk-...`)

### 2. Configurar o ChatTwo

1. Abra o ChatTwo (chat do jogo)
2. Clique no ícone de engrenagem ⚙️ (Settings)
3. Vá para a aba **Miscellaneous**
4. Role até a seção **AI Translation**
5. Configure:

   - ✅ **Enable translation features** → Marque esta opção
   - ✅ **Use OpenAI API directly** → Marque esta opção
   - 🔑 **OpenAI API Key** → Cole sua chave (sk-...)
   - 🤖 **OpenAI Model** → Use `gpt-4o-mini` (recomendado, mais barato) ou `gpt-4`
   - 🌐 **OpenAI API URL** → Deixe o padrão: `https://api.openai.com/v1/chat/completions`

### 3. Configurar Idiomas

Ainda na seção **AI Translation**:

- **Translate incoming chat to target language** → Marque para traduzir mensagens que você recebe
  - **Incoming target language** → Digite `pt-BR` (português brasileiro)
  
- **Translate outgoing chat to target language** → Marque para traduzir mensagens que você envia
  - **Outgoing target language** → Digite `en` (inglês) ou outro idioma

### 4. Testar

1. Salve as configurações
2. Recarregue o plugin ChatTwo no Dalamud
3. Envie ou receba uma mensagem de chat
4. A tradução aparecerá abaixo da mensagem original

## Modelos Recomendados

| Modelo | Velocidade | Qualidade | Custo | Recomendação |
|--------|-----------|-----------|-------|--------------|
| `gpt-4o-mini` | ⚡⚡⚡ Rápido | ✨✨✨ Ótima | 💰 Barato | ✅ **Recomendado** |
| `gpt-4o` | ⚡⚡ Médio | ✨✨✨✨ Excelente | 💰💰 Médio | Para máxima qualidade |
| `gpt-3.5-turbo` | ⚡⚡⚡ Rápido | ✨✨ Boa | 💰 Muito barato | Para economia |

## Códigos de Idioma (BCP-47)

Use estes códigos nos campos de idioma:

- `en` - Inglês
- `pt-BR` - Português (Brasil)
- `pt` - Português (Portugal)
- `es` - Espanhol
- `fr` - Francês
- `de` - Alemão
- `ja` - Japonês
- `ko` - Coreano
- `zh` - Chinês

## Verificando Logs

Se a tradução não estiver funcionando:

1. Abra o **Dalamud Console** (ícone no canto superior direito)
2. Procure por mensagens de log relacionadas a "Translation" ou "OpenAI"
3. Você verá mensagens como:
   - `Using OpenAI direct translation` ✅
   - `Sending OpenAI request for text: '...'` 
   - `OpenAI translation: 'Hello' -> 'Olá'` ✅

### Possíveis Erros

**"OpenAI API key is not configured"**
→ Você não inseriu a API key. Configure-a nas settings.

**"OpenAI API error: 401"**
→ Sua API key está incorreta ou expirou.

**"OpenAI API error: 429"**
→ Você excedeu o limite de requisições ou créditos.

**"Failed to translate via OpenAI"**
→ Erro de conexão com a API. Verifique sua internet.

## Modo Fallback (IPC)

Se você **desmarcar** a opção "Use OpenAI API directly", o ChatTwo voltará a usar o modo IPC (plugin externo), útil se você quiser usar outro serviço de tradução.

## Custos Estimados

Com o modelo `gpt-4o-mini`:
- 1000 mensagens curtas ≈ $0.01 USD
- 1000 mensagens médias ≈ $0.03 USD

É extremamente barato para uso casual!

## Segurança

- Sua API key é armazenada localmente no arquivo de configuração do ChatTwo
- As mensagens são enviadas apenas para a OpenAI (https://api.openai.com)
- Nenhum dado é compartilhado com terceiros
- Configure um limite de gastos na sua conta OpenAI para evitar surpresas

## Suporte

Se tiver problemas, verifique:
1. ✅ Plugin compilado e instalado
2. ✅ Configurações salvas
3. ✅ API key válida
4. ✅ Créditos disponíveis na conta OpenAI
5. ✅ Conexão com internet funcionando

Boa sorte e aproveite a tradução em tempo real! 🎮🌐
