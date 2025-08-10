# Configuration Setup

This project requires certain API keys and secrets to function properly. For security reasons, these are not included in the repository.

## Setup Instructions

### 1. MyNextBook Application Configuration

1. Copy `MyNextBook/appsettings.example.json` to `MyNextBook/appsettings.json`
2. Replace the placeholder values with your actual API keys:
   - `AzureOpenAI.ApiKey`: Your Azure OpenAI API key
   - `Google.ApiKey`: Your Google API key
   - `Google.SearchEngineId`: Your Google Search Engine ID
   - `Syncfusion.LicenseKey`: Your Syncfusion license key

### 2. Test Configuration

1. Copy `ImportSeries.Tests/test.example.runsettings` to `ImportSeries.Tests/test.runsettings`
2. Replace the placeholder values with your actual API keys:
   - `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key
   - `GOOGLE_API_KEY`: Your Google API key
   - `GOOGLE_SEARCH_ENGINE_ID`: Your Google Search Engine ID

## Important Notes

- **The application will not run properly without valid API keys in `appsettings.json`**
- **Syncfusion will show trial popups if no valid license key is provided**
- Never commit actual API keys to the repository
- The `.gitignore` file is configured to exclude files that might contain secrets
- Use environment variables or Azure Key Vault in production environments
- Keep your API keys secure and rotate them regularly

## Security Notes

- Never commit actual API keys to the repository
- The `.gitignore` file is configured to exclude files that might contain secrets
- Use environment variables or Azure Key Vault in production environments
- Keep your API keys secure and rotate them regularly

## Environment Variables

As an alternative to configuration files, you can set the following environment variables:

- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_DEPLOYMENT_NAME`
- `GOOGLE_API_KEY`
- `GOOGLE_SEARCH_ENGINE_ID`

## Getting API Keys

### Azure OpenAI
1. Go to [Azure Portal](https://portal.azure.com)
2. Create or navigate to your Azure OpenAI resource
3. Go to "Keys and Endpoint" to get your API key and endpoint

### Google API
1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Enable the Custom Search API
3. Create credentials (API key)
4. Set up a Custom Search Engine to get the Search Engine ID

### Syncfusion License
1. Go to [Syncfusion](https://www.syncfusion.com)
2. Create a free account or purchase a license
3. Navigate to your account dashboard
4. Find your license key in the "License & Downloads" section
5. Copy the license key to your configuration

**Note**: Syncfusion offers a free community license for individual developers and small businesses. If you don't have a license, the application will show trial popups.

## Troubleshooting

### Syncfusion Trial Popup Issue
If you're still seeing "trial version" popups after adding your license key:

1. Ensure the license key in `appsettings.json` is exactly as provided by Syncfusion
2. Clean and rebuild the solution
3. Check that the `appsettings.json` file is properly embedded as a resource in the project
4. Verify that your Syncfusion license is still valid and not expired

### Configuration Not Loading
If API keys seem to not be loading:

1. Verify that `appsettings.json` exists in the MyNextBook project folder
2. Check that the file's Build Action is set to "EmbeddedResource"
3. Ensure the JSON format is valid (use a JSON validator if needed)
4. Make sure there are no extra characters or spaces in the key values