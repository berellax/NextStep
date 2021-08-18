using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProviderSearch.Models;
using ProviderSearch.Shared;
using ProviderSearch.Activities;
using ProviderSearch.Responses;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ProviderSearch
{
    public class ProviderSearch
    {
        private readonly HttpClient _client;
        private readonly Config _config;

        #region Public Methods
        public ProviderSearch(IHttpClientFactory httpClientFactory)
        {
            _config = new Config(Environment.GetEnvironmentVariables());
            _client = httpClientFactory.CreateClient("base");
        }

        [FunctionName("ProviderSearch")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("ProviderSearch function request initiated.");

            //Parse the Provider Search Request object.
            var searchRequest = await ParseProviderSearchRequest(req);
            log.LogInformation($"Provider Search Request - Zip: {searchRequest.ZipCode}; Radius: {searchRequest.MilesRadius}; ContactId: {searchRequest.ContactId}");

            //Get client app authentication token
            log.LogInformation("Authenticating function app with Dataverse environment.");
            await AuthenticateClient(log);
            log.LogInformation("Authenticated Function with Dataverse successfully");

            //Get Match Scoring Rules
            log.LogInformation("Retrieving Match Scoring Rules");
            ODataResponse<ODataMatchScoringRule> matchRules = await GetMatchScoringRules(log);
            log.LogInformation($"Successfully retrieved {matchRules.Response.Count}");

            //Get Account Objects with Clinical and Residential Options
            log.LogInformation("Retrieving accounts based on geo code with preferences");
            List<ODataAccount> accounts = await GetAccountsByRadius(searchRequest.ZipCode, searchRequest.MilesRadius, log);
            log.LogInformation($"Successfully retrieved {accounts.Count} accounts based on geo code.");

            //Get the Contact Object with Clinical and Residential Options
            log.LogInformation("Retrieving contact for logged in user.");
            ODataContact contact = await GetContact(searchRequest.ContactId, log);
            log.LogInformation("Successfully retrieved Contact");

            //Get the Providers that match the Contact
            log.LogInformation("Matching Providers with Contact based on Options and Preferences");
            List<ProviderResponse> providerResponses = await GetSearchResultProviderResponse(accounts, contact, matchRules, log);
            log.LogInformation($"{providerResponses.Count} Providers matched the Contact on Options and Preferences");

            //Create Search Response
            log.LogInformation("Creating search response based on Matching Providers and Contacts");
            ProviderSearchResponse searchResponse = await CreateSearchResponse(providerResponses, searchRequest.ResultCount, log);
            log.LogInformation($"{searchResponse.Providers.Count} Providers will be returned as part of the search response.");

            return new OkObjectResult(searchResponse);
        }
        #endregion Public Methods

        #region Private Methods

        private async Task<ProviderSearchRequest> ParseProviderSearchRequest(HttpRequest req)
        {
            var facilitySearchRequest = await req.DeserializeRequestBodyAsync<ProviderSearchRequest>();

            return facilitySearchRequest;
        }

        #region Authenticate
        /// <summary>
        /// Authenticate with Dataverse as client application
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task AuthenticateClient(ILogger log)
        {
            log.LogInformation("Begin authenticating client application.");

            try
            {
                var authenticationResult = await Authentication.AuthenticateClient(_config);
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult.AccessToken);
                log.LogInformation("Client authentication token established.");
            }
            catch (Exception e)
            {
                log.LogError($"An error occurred authenticating the client application. ErrorMessage: {e.Message}");
                throw;
            }
        }
        #endregion Authenticate

        #region Get Providers
        /// <summary>
        /// Retrieve accounts within the search radius of the provided zip code
        /// and add in the profile options for those accounts
        /// </summary>
        /// <param name="zipCode"></param>
        /// <param name="milesRadius"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<List<ODataAccount>> GetAccountsByRadius(string zipCode, int milesRadius, ILogger log)
        {
            try
            {
                //Get Geographic Radius for Zip Code
                log.LogInformation($"Getting coordinates and mix/max lat/long for zip code {zipCode}");
                GeoSearch geoSearch = new GeoSearch();
                GeoPoint geoPoint = await geoSearch.GetCoordinatesForZipCode(zipCode);
                GeoRange geoRange = geoSearch.GetMinMaxLatLong(geoPoint, milesRadius);
                log.LogInformation($"Coordinates are Lat: {geoPoint.Latitude}; Long: {geoPoint.Longitude}");
                log.LogInformation($"Radius is Max Lat: {geoRange.LatitudeMax}; Min Lat: {geoRange.LatitudeMin}; Max Long: {geoRange.LongitudeMax}; Min Long: {geoRange.LongitudeMin}");

                //Get Accounts Within Geographic Radius
                log.LogInformation("Retrieving accounts within the geo radius defined.");
                ODataAccount oDataAccount = new ODataAccount();
                ODataResponse<ODataAccount> accounts = await oDataAccount.GetAccounts(_client, geoRange);
                log.LogInformation($"{accounts.Response.Count} accounts retrieved");

                //Get Profile Options For Each Account
                //TODO: Refactor this to use $expand operator on account
                log.LogInformation("Adding profile options to accounts.");
                foreach (var account in accounts.Response)
                {
                    if (account.ClinicalProfileId != null)
                    {
                        Dictionary<string, bool> clinicalProfileOptions = await GetClinicalProfileOptions(account.ClinicalProfileId, true, log);
                        account.ClinicalOptions = clinicalProfileOptions;
                    }
                    if (account.ResidentialProfileId != null)
                    {
                        Dictionary<string, bool> residentialProfileOptions = await GetResidentialProfileOptions(account.ResidentialProfileId, true, log);
                        account.ResidentialOptions = residentialProfileOptions;
                    }
                }
                log.LogInformation("Profile options added to all accounts.");

                //Return account response.
                return accounts.Response;
            }
            catch (Exception ex)
            {
                log.LogError($"An exception occurred getting Accounts by radius. {ex.Message}");
                throw;
            }
        }
        #endregion Get Providers

        #region Get Match Scoring Rules
        /// <summary>
        /// Retrieve Match Scoring Rules
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<ODataResponse<ODataMatchScoringRule>> GetMatchScoringRules(ILogger log)
        {
            try
            {
                ODataMatchScoringRule matchScoringRule = new ODataMatchScoringRule();
                ODataResponse<ODataMatchScoringRule> response = await matchScoringRule.GetMatchScoringRules(_client);

                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"An exception occurred getting Match Scoring Rules. {ex.Message}");
                throw;
            }
        }
        #endregion Get Match Scoring Rules

        #region Get Contact
        /// <summary>
        /// Get the contact record and related profile options for the logged on user based on Contact Id
        /// </summary>
        /// <param name="contactId"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<ODataContact> GetContact(string contactId, ILogger log)
        {
            try
            {
                log.LogInformation($"Retrieving Contact with Id: {contactId}");
                ODataContact contact = new ODataContact();
                contact = await contact.GetContacts(_client, contactId);
                log.LogInformation("Contact retrieved successfully");

                log.LogInformation("Adding profile options to contact.");
                if (contact.ClinicalProfile != null)
                {
                    Dictionary<string, bool> clinicalProfileOptions = await GetClinicalProfileOptions(contact.ClinicalProfile, false, log);
                    contact.ClinicalOptions = clinicalProfileOptions;
                }
                if(contact.ResidentialProfile != null)
                {
                    Dictionary<string, bool> residentialProfileOptions = await GetResidentialProfileOptions(contact.ResidentialProfile, false, log);
                    contact.ResidentialOptions = residentialProfileOptions;
                }
                log.LogInformation("Profile options added to contact.");

                return contact;
            }
            catch (Exception ex)
            {
                log.LogError($"An exception occurred getting contact id {contactId}. {ex.Message}");
                throw;
            }            
        }
        #endregion Get Contact

        #region Get Profile Options
        /// <summary>
        /// Gets the related Clinical Profile and returns a Dictionary with only true values.
        /// </summary>
        /// <param name="profileId"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<Dictionary<string, bool>> GetClinicalProfileOptions(string profileId, bool includeFalse, ILogger log)
        {
            Dictionary<string, bool> profileOptions = new Dictionary<string, bool>();

            ODataClinicalProfile profile = new ODataClinicalProfile();
            JObject clinicalProfile = await profile.GetClinicalProfiles(_client, profileId);

            foreach (JProperty element in clinicalProfile.Children())
            {
                bool boolValue;
                if (bool.TryParse(element.Value.ToString(), out boolValue))
                {
                    if (includeFalse || (!includeFalse && boolValue))
                    {
                        profileOptions.Add(element.Name, boolValue);
                    }
                }
            }

            return profileOptions;
        }

        /// <summary>
        /// Gets the related Residential Profile and returns a Dictionary with only true values.
        /// </summary>
        /// <param name="profileId"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<Dictionary<string, bool>> GetResidentialProfileOptions(string profileId, bool includeFalse, ILogger log)
        {
            Dictionary<string, bool> profileOptions = new Dictionary<string, bool>();

            ODataResidentialProfile profile = new ODataResidentialProfile();
            JObject residentialProfile = await profile.GetResidentialProfiles(_client, profileId);

            foreach (JProperty element in residentialProfile.Children())
            {
                bool boolValue;
                if (bool.TryParse(element.Value.ToString(), out boolValue))
                {
                    if (includeFalse || (!includeFalse && boolValue))
                    {
                        profileOptions.Add(element.Name, boolValue);
                    }
                }
            }

            return profileOptions;
        }
        #endregion Get Profile Options

        #region Create Search Results
        private async Task<ProviderSearchResponse> CreateSearchResponse(List<ProviderResponse> providerResponses, int resultCount, ILogger log)
        {
            ProviderSearchResponse searchResponse = new ProviderSearchResponse();

            if (providerResponses.Any())
            {
                searchResponse.Providers = providerResponses.OrderByDescending(p => p.ProfileScore).Take(resultCount).ToList();
            }
            else
            {
                ProviderResponse providerResponse = new ProviderResponse()
                {
                    Id = Guid.Empty.ToString(),
                    Name = "Test Provider",
                    Phone = "000-000-0000",
                    Fax = "000-000-0000",
                    EmailAddress = "test@xyz.test",
                    Address1 = "123 Main Street",
                    Address2 = "Unit 500",
                    City = "Phoenix",
                    StateOrProvince = "AZ",
                    PostalCode = "85254",
                    Headline = "This is a headline",
                    CurrentPromotions = "This is the current promotion",
                    ShortDescription = "This is a short description",
                    LongDescription = "This is a long description",
                    ProfileScore = 100,
                    MatchedProfileCriterias = new List<ProfileCriteria>(),
                    UnmatchedProfileCriterias = new List<ProfileCriteria>()
                };

                ProfileCriteria matchedCriteria = new ProfileCriteria()
                {
                    AttributeName = "Onsite LPN"
                };
                providerResponse.MatchedProfileCriterias.Add(matchedCriteria);

                ProfileCriteria unmatchedCriteria = new ProfileCriteria()
                {
                    AttributeName = "Onsite Pharmacy"
                };
                providerResponse.UnmatchedProfileCriterias.Add(unmatchedCriteria);

                searchResponse.Providers = new List<ProviderResponse>();
                searchResponse.Providers.Add(providerResponse);
            }

            return searchResponse;
        }
        
        private async Task<List<ProviderResponse>> GetSearchResultProviderResponse(List<ODataAccount> accounts, ODataContact contact, ODataResponse<ODataMatchScoringRule>matchRules, ILogger log)
        {
            List<ProviderResponse> providerResponses = new List<ProviderResponse>();

            foreach (var account in accounts.Where(a => a.ClinicalProfileId != null && a.ResidentialProfileId != null))
            {
                ProviderResponse providerResponse = new ProviderResponse()
                {
                    Id = account.AccountId,
                    Name = account.Name,
                    Phone = account.Phone,
                    Fax = account.Fax,
                    EmailAddress = account.Email,
                    Address1 = account.Address1,
                    Address2 = account.Address2,
                    City = account.City,
                    StateOrProvince = account.StateOrProvince,
                    PostalCode = account.PostalCode,
                    Headline = account.Headline,
                    CurrentPromotions = account.CurrentPromotions,
                    ShortDescription = account.ShortDescription,
                    LongDescription = account.LongDescription,
                    MatchedProfileCriterias = new List<ProfileCriteria>(),
                    UnmatchedProfileCriterias = new List<ProfileCriteria>()
                };

                //Get Matched/Unmatched Clinical Profile Criteria
                List<ProfileCriteria> matchedClinicalProfileCriteria;
                List<ProfileCriteria> unmatchedClinicalProfileCriteria;
                
                GetMatchedProfileCriteria(account.ClinicalOptions, contact.ClinicalOptions, out matchedClinicalProfileCriteria, out unmatchedClinicalProfileCriteria);
                providerResponse.MatchedProfileCriterias.AddRange(matchedClinicalProfileCriteria);
                providerResponse.UnmatchedProfileCriterias.AddRange(unmatchedClinicalProfileCriteria);

                //Get Matched/Unmatched Residential Profile Criteria
                List<ProfileCriteria> matchedResidentialProfileCriteria;
                List<ProfileCriteria> unmatchedResidentialProfileCriteria;
                
                GetMatchedProfileCriteria(account.ClinicalOptions, contact.ResidentialOptions, out matchedResidentialProfileCriteria, out unmatchedResidentialProfileCriteria);
                providerResponse.MatchedProfileCriterias.AddRange(matchedResidentialProfileCriteria);
                providerResponse.UnmatchedProfileCriterias.AddRange(unmatchedResidentialProfileCriteria);

                //Calculate Score
                List<ODataMatchScoringRule> clinicalMatchRules = matchRules.Response.Where(m => m.TargetProfileType == (int)MatchProfileType.Clinical).ToList<ODataMatchScoringRule>();

                List<ODataMatchScoringRule> residentialMatchRules = matchRules.Response.Where(m => m.TargetProfileType == (int)MatchProfileType.Residential).ToList<ODataMatchScoringRule>();

                int profileScore = 0;
                foreach(var match in matchedClinicalProfileCriteria)
                {
                    profileScore += clinicalMatchRules.Where(m => m.Field.ToLower() == match.AttributeName.ToLower()).Sum(r => r.Score);
                }

                foreach(var match in matchedResidentialProfileCriteria)
                {
                    profileScore += residentialMatchRules.Where(m => m.Field.ToLower() == match.AttributeName.ToLower()).Sum(r => r.Score);
                }

                providerResponse.ProfileScore = profileScore;

                //Add Response to List
                providerResponses.Add(providerResponse);
            }

            return providerResponses;
        }

        private void GetMatchedProfileCriteria(Dictionary<string, bool> accountOptions, Dictionary<string, bool> contactOptions, 
                                                out List<ProfileCriteria> matchedCriteria, out List<ProfileCriteria> unmatchedCriteria)
        {
            matchedCriteria = new List<ProfileCriteria>();
            unmatchedCriteria = new List<ProfileCriteria>();

            foreach (var(key, accountValue) in accountOptions)
            {
                if (contactOptions.ContainsKey(key) && Equals(accountValue, contactOptions[key]))
                {
                    ProfileCriteria criteria = new ProfileCriteria()
                    {
                        AttributeName = key
                    };
                    matchedCriteria.Add(criteria);
                }
                else
                {
                    ProfileCriteria criteria = new ProfileCriteria()
                    {
                        AttributeName = key
                    };
                    unmatchedCriteria.Add(criteria);
                }
            }
        }

        private bool DoesDictionaryMatch(Dictionary<string, bool> accountOptions, Dictionary<string, bool> contactOptions)
        {
            bool isMatch = false;
            
            var result = new Dictionary<string, bool>(accountOptions.Count, accountOptions.Comparer);

            foreach(var (key, value) in contactOptions)
            {
                if (accountOptions.ContainsKey(key))
                {
                    var contactValue = accountOptions[key];

                    if(Equals(value, contactValue))
                    {
                        isMatch = true;
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return isMatch;
        }
        #endregion Create Search Results

        //private async Task<Dictionary<string, bool>> GetCombinedProfile(JToken clinicalProfile, JToken residentialProfile)
        //{
        //    Dictionary<string, bool> combinedProfile = new Dictionary<string, bool>();

        //    foreach (JProperty element in clinicalProfile.Children())
        //    {
        //        if (element.Value.ToString().ToLower() == "true")
        //        {
        //            combinedProfile.Add(element.Name, Boolean.Parse(element.Value.ToString()));
        //        }
        //    }

        //    foreach (JProperty element in residentialProfile.Children())
        //    {
        //        if(element.Value.ToString().ToLower() == "true")
        //        {
        //            combinedProfile.Add(element.Name, Boolean.Parse(element.Value.ToString()));
        //        }
        //    }

        //    return combinedProfile;
        //}

        #endregion Private Methods

    }
}
