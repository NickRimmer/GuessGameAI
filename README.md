# GuessGameAI
Telegram bot for 'Guess The Word' game

## How to run

- Copy `src/FoolMeGame/appsettings.json` file to `src/FoolMeGame/appsettings.local.json`
- Edit `src/FoolMeGame/appsettings.local.json`
  - Set `ApiKeySettings.ApiKeys.Key` value with your random key
  - Set `TelegramSettings.Token` from @BotFather with your bot access token
  - Set `OpenAISettings.ApiKey` with your OpenAI access key
- Update constants `src/FoolMeGame/Constants.cs`, set `GlobalAdminUserId` your Telegram user id (TBD - add it to appsettings)
 - Register Telegram webhook to your server `POST https://api.telegram.org/bot{TelegramSettings.Token}/setWebhook?url={url_to_send_updates_to}`
 - Make sure your Bot has turned off `Group Policy` or add Bot permissions in group to read messages
 - Build and Run `src/FoolMeGame/FoolMeGame.csproj`
 - Create a group in Telegram
   - Add your Bot to the group
   - Make sure Bot has access to messages
   - Send `/register` command, to allow playing with bot in this group
   - Send `/settings` to see available settings
   - Send `/new` to start a new game
