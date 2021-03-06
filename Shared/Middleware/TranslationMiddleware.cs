using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Shared.ApiInterface;
using Shared.Models;
using Shared.Translation;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Middleware
{
    /// <summary>
    /// Middleware for translating text between the user and bot.
    /// Uses the Microsoft Translator Text API.
    /// </summary>
    public class TranslationMiddleware : IMiddleware
    {
        private readonly IStatePropertyAccessor<UserProfileState> _userProfileState;
        Translator translator;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslationMiddleware"/> class.
        /// </summary>
        public TranslationMiddleware(IServiceProvider serviceProvider, Translator translator)
        {
            UserState userState = serviceProvider.GetService<UserState>();
            _userProfileState = userState.CreateProperty<UserProfileState>(nameof(UserProfileState));
            
            this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        }

        /// <summary>
        /// Processes an incoming activity.
        /// </summary>
        /// <param name="turnContext">Context object containing information for a single turn of conversation with a user.</param>
        /// <param name="next">The delegate to call to continue the bot middleware pipeline.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            await HandleIncomingMessage(turnContext, cancellationToken);

            turnContext.OnSendActivities(async (newContext, activities, nextSend) =>
            {
                await HandleOutgoingMessage(activities, newContext, cancellationToken);
                return await nextSend();
            });

            turnContext.OnUpdateActivity(async (newContext, activity, nextUpdate) =>
            {
                await HandleOutgoingMessage(new List<Activity> { activity }, newContext, cancellationToken);
                return await nextUpdate();
            });

            // Invoke the next middleware.
            await next(cancellationToken).ConfigureAwait(false);
        }

        private async Task HandleIncomingMessage(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!this.translator.IsConfigured || turnContext.Activity.Type != ActivityTypes.Message)
            {
                return;
            }

            // Store the original incoming text so that it is still available after translation.
            turnContext.Activity.ChannelData = turnContext.Activity.Text;

            var userProfile = await _userProfileState.GetAsync(turnContext, () => new UserProfileState(), cancellationToken);

            // Translate messages sent from the user to the default language.
            if (userProfile.Language != Translator.DefaultLanguage)
            {
                // For the first message we need to detect the user's language and store it.
                if (string.IsNullOrEmpty(userProfile.Language))
                {
                    var result = await this.translator.TranslateToDataAsync(turnContext.Activity.Text, Translator.DefaultLanguage, cancellationToken);
                    turnContext.Activity.Text = result?.Translations?.FirstOrDefault()?.Text;

                    userProfile.Language = result?.DetectedLanguage?.Language ?? Translator.DefaultLanguage;
                    await _userProfileState.SetAsync(turnContext, userProfile, cancellationToken);
                }
                else
                {
                    await TranslateAsync(turnContext.Activity, Translator.DefaultLanguage, cancellationToken);
                }
            }
        }

        private async Task HandleOutgoingMessage(List<Activity> activities, ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!this.translator.IsConfigured)
            {
                return;
            }

            var userProfile = await _userProfileState.GetAsync(turnContext, () => new UserProfileState(), cancellationToken);

            // User can be null if they do not consent and their record is deleted.
            if (userProfile == null)
            {
                return;
            }

            // Translate messages sent to the user to the user's language.
            if (userProfile.Language != null && userProfile.Language != Translator.DefaultLanguage)
            {
                List<Task> tasks = new List<Task>();
                foreach (Activity currentActivity in activities.Where(a => a.Type == ActivityTypes.Message))
                {
                    tasks.Add(TranslateAsync(currentActivity, userProfile.Language, cancellationToken));
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
        }

        private async Task TranslateAsync(Activity activity, string targetLocale, CancellationToken cancellationToken)
        {
            activity.Text = await this.translator.TranslateAsync(activity.Text, targetLocale, cancellationToken);
        }
    }
}