using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace ProviderSearch.Models
{
    internal class ProviderResponse : ModelProcessing
    {
        private string _id;
        private string _name;
        private string _phone;
        private string _fax;
        private string _emailAddress;
        private string _address1;
        private string _address2;
        private string _city;
        private string _stateOrProvince;
        private string _postalCode;
        private string _headline;
        private string _currentPromotions;
        private string _shortDescription;
        private string _longDescription;
        private List<ProviderMedia> _medialUrls;
        private int _profileScore;
        private List<ProfileCriteria> _matchedProfileCriterias;
        private List<ProfileCriteria> _unmatchedProfileCriterias;

        [JsonPropertyName("id")]
        public string Id
        {
            get { return _id; }
            set { _id = _encode(value); }
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get { return _name; }
            set { _name = _encode(value); }
        }

        [JsonPropertyName("phone")]
        public string Phone
        {
            get { return _phone; }
            set { _phone = _encode(value); }
        }

        [JsonPropertyName("fax")]
        public string Fax
        {
            get { return _fax; }
            set { _fax = _encode(value); }
        }

        [JsonPropertyName("emailAddress")]
        public string EmailAddress
        {
            get { return _emailAddress; }
            set { _emailAddress = _encode(value); }
        }

        [JsonPropertyName("address1")]
        public string Address1
        {
            get { return _address1; }
            set { _address1 = _encode(value); }
        }

        [JsonPropertyName("address2")]
        public string Address2
        {
            get { return _address2; }
            set { _address2 = _encode(value); }
        }

        [JsonPropertyName("city")]
        public string City
        {
            get { return _city; }
            set { _city = _encode(value); }
        }

        [JsonPropertyName("stateOrProvince")]
        public string StateOrProvince
        {
            get { return _stateOrProvince; }
            set { _stateOrProvince = _encode(value); }
        }

        [JsonPropertyName("postalCode")]
        public string PostalCode
        {
            get { return _postalCode; }
            set { _postalCode = _encode(value); }
        }

        [JsonPropertyName("headline")]
        public string Headline
        {
            get { return _headline; }
            set { _headline = _encode(value); }
        }

        [JsonPropertyName("currentPromotion")]
        public string CurrentPromotions
        {
            get { return _currentPromotions; }
            set { _currentPromotions = _encode(value); }
        }

        [JsonPropertyName("shortDescription")]
        public string ShortDescription
        {
            get { return _shortDescription; }
            set { _shortDescription = _encode(value); }
        }

        [JsonPropertyName("longDescription")]
        public string LongDescription
        {
            get { return _longDescription; }
            set { _longDescription = _encode(value); }
        }

        //TODO: Figure out encoding for collection
        [JsonPropertyName("mediaUrls")]
        public List<ProviderMedia> MediaUrls
        {
            get { return _medialUrls; }
            set { _medialUrls = value; }
        }

        [JsonPropertyName("profileScore")]
        public int ProfileScore
        {
            get { return _profileScore; }
            set { _profileScore = value; }
        }

        //TODO: Figure out encoding for collection
        [JsonPropertyName("matchedProfileCriteria")]
        public List<ProfileCriteria> MatchedProfileCriterias
        {
            get { return _matchedProfileCriterias; }
            set { _matchedProfileCriterias = value; }
        }

        //TODO: Figure out encoding for collection
        [JsonPropertyName("unmatchedProfileCriteria")]
        public List<ProfileCriteria> UnmatchedProfileCriterias
        {
            get { return _unmatchedProfileCriterias; }
            set { _unmatchedProfileCriterias = value; }
        }
    }

    internal class ProviderMedia : ModelProcessing
    {
        private int _urlId;
        private string _urlType;
        private string _urlValue;
        private string _urlDesc;

        [JsonPropertyName("urlId")]
        public int UrlId
        {
            get { return _urlId; }
            set { _urlId = value; }
        }

        [JsonPropertyName("urlType")]
        public string UrlType
        {
            get { return _urlType; }
            set { _urlType = _encode(value); }
        }

        [JsonPropertyName("urlValue")]
        public string UrlValue
        {
            get { return _urlValue; }
            set { _urlValue = _encode(value); }
        }

        [JsonPropertyName("urlDesc")]
        public string UrlDescription
        {
            get { return _urlDesc; }
            set { _urlDesc = _encode(value); }
        }
    }

    internal class ProfileCriteria : ModelProcessing
    {
        private int _id;
        private string _attributeName;

        [JsonPropertyName("id")]
        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        [JsonPropertyName("attributeName")]
        public string AttributeName
        {
            get { return _attributeName; }
            set { _attributeName = _encode(value); }
        }
    }
}
