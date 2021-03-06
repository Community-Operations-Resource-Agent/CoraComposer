using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Configuration;
using Shared.ApiInterface;

namespace Shared.Prompts
{
    public static class Prompt
    {
        public static string ChoicePrompt = "ChoicePrompt";
        public static string ConfirmPrompt = "ConfirmPrompt";
        public static string IntPrompt = "IntPrompt";
        public static string TextPrompt = "TextPrompt";
        public static string KeywordTextPrompt = "KeywordTextPrompt";
        public static string LocationTextPrompt = "LocationTextPrompt";
        public static string HourPrompt = "HourPrompt";
        public static string HourMinutePrompt = "HourMinutePrompt";
        public static string DaysPrompt = "DaysPrompt";


        /// <summary>
        /// Adds each prompt to the master dialog set
        /// </summary>
        public static void Register(DialogSet dialogs, IConfiguration configuration, IApiInterface api)
        {

            dialogs.Add(new CustomChoicePrompt(ChoicePrompt));
            dialogs.Add(new CustomConfirmPrompt(ConfirmPrompt));
            dialogs.Add(new NumberPrompt<int>(IntPrompt, NotNegativeIntPromptValidator.Create()));
            dialogs.Add(new TextPrompt(TextPrompt));
            dialogs.Add(new TextPrompt(LocationTextPrompt, LocationPromptValidator.Create(configuration)));
        }
    }
}
