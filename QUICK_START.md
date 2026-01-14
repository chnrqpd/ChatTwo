# 🚀 Guia Rápido de Instalação

## Passo 1: Compilar
```bash
cd c:\Users\chnrq\Documents\ChatTwo
dotnet build ChatTwo.sln -c Release
```

## Passo 2: Instalar
Copie o DLL compilado para a pasta de plugins do Dalamud:
```
ChatTwo\bin\Release\ChatTwo.dll
→ %AppData%\XIVLauncher\devPlugins\ChatTwo\
```

## Passo 3: Recarregar no Jogo
1. Abra o jogo e o Dalamud
2. Digite `/xlplugins` no chat
3. Encontre "ChatTwo" e clique em "Reload"

## Passo 4: Configurar OpenAI

### Opção A: Usar sua própria API Key
1. Obtenha sua API Key em https://platform.openai.com
2. Abra ChatTwo Settings → Miscellaneous
3. Marque "Enable translation features"
4. Marque "Use OpenAI API directly"
5. Cole sua API Key
6. Configure idiomas (ex: incoming=pt-BR, outgoing=en)
7. Salve

### Opção B: Usar Plugin IPC (modo antigo)
1. Instale um plugin de tradução compatível
2. Abra ChatTwo Settings → Miscellaneous
3. Marque "Enable translation features"
4. **DESMARQUE** "Use OpenAI API directly"
5. Configure o IPC name
6. Salve

## Passo 5: Testar
1. Envie ou receba mensagens no chat
2. A tradução aparecerá abaixo da mensagem original
3. Verifique logs no Dalamud Console se houver problemas

## Solução de Problemas

### Tradução não aparece
- ✅ Verifique se "Enable translation features" está marcado
- ✅ Verifique se API Key está correta
- ✅ Verifique logs no Dalamud Console
- ✅ Aguarde alguns segundos (tradução é assíncrona)

### Erro 401 (Unauthorized)
- ❌ API Key inválida ou expirada
- ✅ Gere uma nova key em platform.openai.com

### Erro 429 (Rate Limit)
- ❌ Muitas requisições ou sem créditos
- ✅ Configure billing em platform.openai.com
- ✅ Adicione créditos à sua conta

### Plugin não carrega
- ✅ Recompile com `dotnet build ChatTwo.sln -c Release`
- ✅ Verifique se copiou o DLL correto
- ✅ Recarregue o plugin no Dalamud

## Custos
Com gpt-4o-mini (recomendado):
- ~$0.00004 por mensagem
- ~$0.04 por 1000 mensagens
- Extremamente barato para uso pessoal!

---

**Pronto!** Agora você tem tradução automática funcionando no chat do FFXIV! 🎮✨
