﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ExactTarget.TriggeredEmail.Core;
using ExactTarget.TriggeredEmail.Core.Configuration;
using ExactTarget.TriggeredEmail.Core.RequestClients.DataExtension;
using ExactTarget.TriggeredEmail.Core.RequestClients.DeliveryProfile;
using ExactTarget.TriggeredEmail.Core.RequestClients.Email;
using ExactTarget.TriggeredEmail.Core.RequestClients.EmailTemplate;
using ExactTarget.TriggeredEmail.Core.RequestClients.TriggeredSendDefinition;
using Priority = ExactTarget.TriggeredEmail.Trigger.Priority;

namespace ExactTarget.TriggeredEmail.Creation
{
    public class DynamicTriggeredEmailCreator : IDynamicTriggeredEmailCreator
    {
        private readonly IDataExtensionClient _dataExtensionClient;
        private readonly IDeliveryProfileClient _deliveryProfileClient;
        private readonly IEmailRequestClient _emailRequestClient;
        private readonly IEmailTemplateClient _emailTemplateClient;
        private readonly ITriggeredSendDefinitionClient _triggeredSendDefinitionClient;

        public DynamicTriggeredEmailCreator(IDataExtensionClient dataExtensionClient,
            ITriggeredSendDefinitionClient triggeredSendDefinitionClient,
            IEmailTemplateClient emailTemplateClient,
            IEmailRequestClient emailRequestClient,
            IDeliveryProfileClient deliveryProfileClient)
        {
            _dataExtensionClient = dataExtensionClient;
            _triggeredSendDefinitionClient = triggeredSendDefinitionClient;
            _emailTemplateClient = emailTemplateClient;
            _emailRequestClient = emailRequestClient;
            _deliveryProfileClient = deliveryProfileClient;
        }

        public DynamicTriggeredEmailCreator(IExactTargetConfiguration config)
        {
            _triggeredSendDefinitionClient = new TriggeredSendDefinitionClient(config);
            _dataExtensionClient = new DataExtensionClient(config);
            _emailTemplateClient = new EmailTemplateClient(config);
            _emailRequestClient = new EmailRequestClient(config);
            _deliveryProfileClient = new DeliveryProfileClient(config);
        }

        public int Create(string externalKey, string layoutHtml, Priority priority = Priority.Medium)
        {
            if (externalKey.Length > Guid.Empty.ToString().Length)
            {
                throw new ArgumentException(
                    "externalKey too long, should be max length of " + Guid.Empty.ToString().Length, "externalKey");
            }

            if (_triggeredSendDefinitionClient.DoesTriggeredSendDefinitionExist(externalKey))
            {
                throw new Exception(string.Format("A TriggeredSendDefinition with external key {0} already exsits",
                    externalKey));
            }

            var dataExtensionExternalKey = ExternalKeyGenerator.GenerateExternalKey("data-extension-" + externalKey);
            if (!_dataExtensionClient.DoesDataExtensionExist(dataExtensionExternalKey))
            {
                var dataExtensionTemplateObjectId =
                    _dataExtensionClient.RetrieveTriggeredSendDataExtensionTemplateObjectId();

                var regex = new Regex(@"(?<=%%)[a-zA-Z0-9].*?[a-zA-Z0-9]?(?=%%)");
                var matches = regex.Matches(layoutHtml);
                var dataExtensionFieldNames = new HashSet<string> {"Subject", "Body", "Head"};

                for (var i = 0; i < matches.Count; i++)
                {
                    dataExtensionFieldNames.Add(matches[i].Value);
                }

                _dataExtensionClient.CreateDataExtension(dataExtensionTemplateObjectId,
                    dataExtensionExternalKey,
                    "triggeredsend-" + externalKey,
                    dataExtensionFieldNames);
            }


            var emailTempalteExternalKey = ExternalKeyGenerator.GenerateExternalKey("email-template" + externalKey);
            var emailTemplateId = _emailTemplateClient.RetrieveEmailTemplateId(emailTempalteExternalKey);
            if (emailTemplateId == 0)
            {
                layoutHtml += EmailContentHelper.GetOpenTrackingTag() +
                              EmailContentHelper.GetCompanyPhysicalMailingAddressTags();
                emailTemplateId = _emailTemplateClient.CreateEmailTemplate(emailTempalteExternalKey,
                    "template-" + externalKey, layoutHtml);
            }

            var emailId = _emailRequestClient.CreateEmailFromTemplate(emailTemplateId, "email-" + externalKey,
                "%%Subject%%", new KeyValuePair<string, string>("dynamicArea", "%%Body%%"));

            var deliveryProfileExternalKey = ExternalKeyGenerator.GenerateExternalKey("blank-delivery-profile");
            _deliveryProfileClient.TryCreateBlankDeliveryProfile(deliveryProfileExternalKey);
            var triggeredSendDefinition = _triggeredSendDefinitionClient.CreateTriggeredSendDefinition(externalKey,
                emailId, dataExtensionExternalKey, deliveryProfileExternalKey, externalKey, externalKey, priority.ToString());
            return triggeredSendDefinition;
        }
    }
}