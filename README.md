# NovelAPI
An C# API for NovelAI.
Based on work from [wbrown's NovelAI Research Tool](https://github.com/wbrown/novelai-research-tool).

Built against .Net Framework 4.6.1
Requires Konscious's Argon2 (v1.2.0 known good) and RestSharp (v106.12.0.0 known good) available via NuGet.

## Setup
Authentication is broken right now. You will need to use your browser console to extract your authentication token from the login request when logging into the NovelAI.net site. Then change Auth.cs line 97 to read:

	keys.AccessToken = "{your token here}";

Then compile to .dll or include in your solution directly.

#### How it should work:
On first run, Auth will generate a file `./config/auth.json` relative to your application's exe that contains default `<empty>` credentials. Edit the file to include your actual log in details, save, re-run your application. Note that generating the auth_keys is a time-intensive cryptographic process and it may take your application a minute or more to generate.

## Using the API
The API is async and getting responses from the AI is simple:

	NovelAPI api = NovelAPI.NewNovelAiAPI();
	if(api != null) {
		string response = await api.GenerateAsync("Hello World!");
		Console.WriteLine(response);
	}
NovelAPI does not handle context for you, so you will need to pass full context, not just prompt input to the `GenerateAsync()` method.

## TODO
 - Fix the stupid auth_key problem.
 - Save the auth token to disk and re-use it on later runs instead of recreating the auth_key.