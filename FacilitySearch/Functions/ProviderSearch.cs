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
            try
            {

                log.LogInformation("ProviderSearch function request initiated.");

                //Parse the Provider Search Request object.
                var searchRequest = await ParseProviderSearchRequest(req);
                log.LogInformation($"Provider Search Request - Zip: {searchRequest.ZipCode}; Radius: {searchRequest.MilesRadius}; ContactId: {searchRequest.ContactId}");

                //Get client app authentication token
                log.LogInformation("Authenticating function app with Dataverse environment.");
                await AuthenticateClient(log);
                log.LogInformation("Authenticated Function with Dataverse successfully");

                //Get the Contact Object with Clinical and Residential Options
                log.LogInformation("Retrieving contact for logged in user.");
                ODataContact contact = await GetContact(searchRequest.ContactId, log);
                log.LogInformation("Successfully retrieved Contact");

                if(contact == null)
                {
                    return new NotFoundObjectResult($"Contact with id {searchRequest.ContactId} was not found.");
                }

                //Get Account Objects with Clinical and Residential Options
                log.LogInformation("Retrieving accounts based on geo code with preferences");
                List<ODataAccount> accounts = await GetAccountsByRadius(searchRequest.ZipCode, searchRequest.MilesRadius, log);
                log.LogInformation($"Successfully retrieved {accounts.Count} accounts based on geo code.");

                if (!accounts.Any())
                {
                    return new NotFoundObjectResult($"No accounts were found for the current query");
                }

                //Get the Providers that match the Contact
                log.LogInformation("Matching Providers with Contact based on Options 6and Preferences");
                List<ProviderResponse> providerResponses = await GetProviderResponses(accounts, contact, log);
                log.LogInformation($"{providerResponses.Count} Providers matched the Contact on Options and Preferences");

                //Create Search Response
                log.LogInformation("Creating search response based on Matching Providers and Contacts");
                ProviderSearchResponse searchResponse = await CreateSearchResponse(providerResponses, searchRequest.ResultCount, log);
                log.LogInformation($"{searchResponse.Providers.Count} Providers will be returned as part of the search response.");

                return new OkObjectResult(searchResponse);
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred in provider search. Message: {ex.Message}. Inner Exception: {ex.InnerException}");
                ProviderSearchResponse response = new ProviderSearchResponse()
                {
                    ErrorMessage = ex.Message,
                    IsError = true
                };

                return new BadRequestObjectResult(response);
            }
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

                if(contact == null)
                {
                    throw new Exception($"Contact with id {contactId} was not found");
                }

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
       
        /// <summary>
        /// Gets the list of Provider Responess to include in teh Search Results
        /// </summary>
        /// <param name="accounts"></param>
        /// <param name="contact"></param>
        /// <param name="matchRules"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private async Task<List<ProviderResponse>> GetProviderResponses(List<ODataAccount> accounts, ODataContact contact, ILogger log)
        {
            //Create a collection of provider responses to populate in this method.
            List<ProviderResponse> providerResponses = new List<ProviderResponse>();

            //Get a collection of accounts with a Clinical and Residential Profile Id
            List<ODataAccount> targetAccounts = accounts.Where(a => a.ClinicalProfileId != null && a.ResidentialProfileId != null).ToList();
            log.LogInformation($"Retrieved {targetAccounts.Count} target accounts to format for search response.");

            //Get Match Scoring Rules
            log.LogInformation("Retrieving Match Scoring Rules");
            ODataResponse<ODataMatchScoringRule> matchRules = await GetMatchScoringRules(log);
            log.LogInformation($"Successfully retrieved {matchRules.Response.Count} Match Scoring Rules");

            //Split Match Scoring Rules
            List<ODataMatchScoringRule> clinicalMatchRules = matchRules.Response.Where(m => m.TargetProfileType == (int)MatchProfileType.Clinical).ToList();
            List<ODataMatchScoringRule> residentialMatchRules = matchRules.Response.Where(m => m.TargetProfileType == (int)MatchProfileType.Residential).ToList();
            log.LogInformation($"{clinicalMatchRules.Count} Clinical Match Rules Identified");
            log.LogInformation($"{residentialMatchRules.Count} Residential Match Ruels Identified");

            //Create Provider Response for each account.
            foreach (var account in targetAccounts)
            {
                log.LogInformation($"Creating Provider Response for Account {account.Name}");
                ProviderResponse providerResponse = CreateProviderResponse(account, contact, clinicalMatchRules, residentialMatchRules, log);
                providerResponses.Add(providerResponse);
                log.LogInformation($"Successfully added Provider Response {account.Name} to search response.");
            }

            return providerResponses;
        }

        /// <summary>
        /// Creates a Provider Response for a given account/contact and match rules
        /// </summary>
        /// <param name="account"></param>
        /// <param name="contact"></param>
        /// <param name="matchRules"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private ProviderResponse CreateProviderResponse(ODataAccount account, ODataContact contact, List<ODataMatchScoringRule> clinicalMatchRules, 
                                                            List<ODataMatchScoringRule> residentialMatchRules, ILogger log)
        {
            //Create a new Provider Response objects and populate account information
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
                UnmatchedProfileCriterias = new List<ProfileCriteria>(),
                ProfileScore = 0,
                MediaUrls = new List<ProviderMedia>()
            };

            log.LogInformation("Setting Matched and Unmatched Profile Criteria for Provider Response");
            SetResponseCriteriaAndScore(providerResponse, account, contact, clinicalMatchRules, residentialMatchRules, log);
            log.LogInformation($"{providerResponse.MatchedProfileCriterias.Count} Matched Profile Criterias and {providerResponse.UnmatchedProfileCriterias.Count} Unmatched Profile Criterias");

            log.LogInformation("Getting related medai and setting the MediaUrls for Provider Response");
            providerResponse.MediaUrls = GetProviderMedia(account, log);
            log.LogInformation($"{providerResponse.MediaUrls.Count} Provider Media added to the Provider Response");

            return providerResponse;
        }

        private List<ProviderMedia> GetProviderMedia(ODataAccount account, ILogger log)
        {
            List<ProviderMedia> providerMedia = new List<ProviderMedia>();
            BlobStorage blob = new BlobStorage(Config.BlobStorageConnectionString, Config.BlobStorageContainerName, log);

            List<string> imageUrls = blob.GetBlobStorageUrlByRecordId(account.AccountId.Replace("-", "").ToLower());

            foreach (string url in imageUrls)
            {
                ProviderMedia media = new ProviderMedia()
                {
                    UrlValue = url
                };

                providerMedia.Add(media);
            }

            if (!providerMedia.Any())
            {
                providerMedia.Add(new ProviderMedia()
                {
                    UrlValue = string.Empty
                });
            }

            return providerMedia;
        }

        /// <summary>
        /// Identify and set the Matched and Unmatched Criteria collections
        /// Set the attribute names for the collection to the friendly name from the attribute name
        /// Calculate and set the profile score based on the matched collection
        /// </summary>
        /// <param name="providerResponse"></param>
        /// <param name="account"></param>
        /// <param name="contact"></param>
        /// <param name="clinicalMatchRules"></param>
        /// <param name="residentialMatchRules"></param>
        /// <param name="log"></param>
        private void SetResponseCriteriaAndScore(ProviderResponse providerResponse, ODataAccount account, ODataContact contact,
                                                                    List<ODataMatchScoringRule> clinicalMatchRules, List<ODataMatchScoringRule> residentialMatchRules, ILogger log)
        {
            //Set Matched/Unmatched Clinical Profile Criteria
            List<ProfileCriteria> matchedClinicalCriteria;
            List<ProfileCriteria> unmatchedClinicalCriteria;
            GetMatchedProfileCriteria(account.ClinicalOptions, contact.ClinicalOptions, out matchedClinicalCriteria, out unmatchedClinicalCriteria);

            //Set Matched/Unmatched Residential Profile Criteria
            List<ProfileCriteria> matchedResidentialCriteria;
            List<ProfileCriteria> unmatchedResidentialCriteria;
            GetMatchedProfileCriteria(account.ResidentialOptions, contact.ResidentialOptions, out matchedResidentialCriteria, out unmatchedResidentialCriteria);

            //Set Profile Score for Clinical and Residential Profile Criteria
            int clinicalScore = CalculateProfileScore(matchedClinicalCriteria, clinicalMatchRules, log);
            int residentialScore = CalculateProfileScore(matchedResidentialCriteria, residentialMatchRules, log);

            //Update Criteria to Friendly Name for Clinical and Residential Profile Criteria
            UpdateCriteriaFriendlyNames(matchedClinicalCriteria, clinicalMatchRules, log);
            UpdateCriteriaFriendlyNames(unmatchedClinicalCriteria, clinicalMatchRules, log);
            UpdateCriteriaFriendlyNames(matchedResidentialCriteria, residentialMatchRules, log);
            UpdateCriteriaFriendlyNames(unmatchedResidentialCriteria, residentialMatchRules, log);

            //Update Provider Response Object
            providerResponse.MatchedProfileCriterias.AddRange(matchedClinicalCriteria);
            providerResponse.MatchedProfileCriterias.AddRange(matchedResidentialCriteria);
            providerResponse.UnmatchedProfileCriterias.AddRange(unmatchedClinicalCriteria);
            providerResponse.UnmatchedProfileCriterias.AddRange(unmatchedResidentialCriteria);
            providerResponse.ProfileScore = clinicalScore + residentialScore;
        }

        /// <summary>
        /// Calculate and return a Profile Score based on Profile Criterias and Match Rules
        /// </summary>
        /// <param name="profileCriterias"></param>
        /// <param name="matchRules"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private int CalculateProfileScore(List<ProfileCriteria> profileCriterias, List<ODataMatchScoringRule> matchRules, ILogger log)
        {
            int profileScore = 0;

            //Match Profile Criteria and Calculate Score
            foreach (var match in profileCriterias)
            {
                var relatedMatchRules = matchRules.Where(m => m.Field.ToLowerInvariant() == match.AttributeName.ToLowerInvariant()).ToList();

                //Check if a Match Rule was found based on Attribute Name and set the match score.
                if(relatedMatchRules.Any())
                {
                    int score = relatedMatchRules.Sum(r => r.Score);
                    profileScore += score;
                    log.LogInformation($"Calculated {score} for {match.AttributeName}.");
                }
                else
                {
                    log.LogInformation($"No Matching Rule found for {match.AttributeName}.");
                }
            }
            
            return profileScore;
        }

        /// <summary>
        /// Update the criteria attributes from the schema name to the friendly name as defined in the Match Rule.
        /// </summary>
        /// <param name="profileCriterias"></param>
        /// <param name="matchRules"></param>
        /// <param name="log"></param>
        private void UpdateCriteriaFriendlyNames(List<ProfileCriteria> profileCriterias, List<ODataMatchScoringRule> matchRules, ILogger log)
        {
            //Match Profile Criteria and Calculate Score
            foreach (var criteria in profileCriterias)
            {
                string criteriaAttribute = criteria.AttributeName;

                var relatedMatchRule = matchRules.Where(m => m.Field.ToLowerInvariant() == criteriaAttribute.ToLowerInvariant()).
                                                    Select(a => new ODataMatchScoringRule()
                                                    {
                                                        Field = a.Field,
                                                        FriendlyName = a.FriendlyName,
                                                    }).FirstOrDefault();
                
                //Check if a Match Rule was found based on Attribute Name and set the Attribute Name
                if (relatedMatchRule == null)
                {
                    log.LogInformation($"No Matching Rule found for {criteria.AttributeName}.");
                }
                else
                {
                    criteria.FriendlyName = relatedMatchRule.FriendlyName;
                    log.LogInformation($"Set Criteria for Attribute {relatedMatchRule.Field} to Friendly Name {relatedMatchRule.FriendlyName}.");
                }
            }
        }

        /// <summary>
        /// Determine the matched and unmatched criteria based on Account and Contact Options
        /// </summary>
        /// <param name="accountOptions"></param>
        /// <param name="contactOptions"></param>
        /// <param name="matchedCriteria"></param>
        /// <param name="unmatchedCriteria"></param>
        private void GetMatchedProfileCriteria(Dictionary<string, bool> accountOptions, Dictionary<string, bool> contactOptions, 
                                                out List<ProfileCriteria> matchedCriteria, out List<ProfileCriteria> unmatchedCriteria)
        {
            matchedCriteria = new List<ProfileCriteria>();
            unmatchedCriteria = new List<ProfileCriteria>();

            if (!contactOptions.Any())
            {
                return;
            }

            foreach (var(key, accountValue) in accountOptions)
            {
                ProfileCriteria criteria = new ProfileCriteria();
                //If the option was selected by the user and matches the provider, set to matched
                if (contactOptions.ContainsKey(key) && contactOptions[key] == accountValue)
                {
                    criteria.AttributeName = key;
                    matchedCriteria.Add(criteria);
                }
                //If the option was selected by the user and does not match the provider, set to unmatched
                else if (contactOptions.ContainsKey(key))
                {
                    criteria.AttributeName = key;
                    unmatchedCriteria.Add(criteria);
                }
            }
        }

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
        #endregion Create Search Results

        #endregion Private Methods

    }
}
