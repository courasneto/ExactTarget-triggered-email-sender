﻿using System;
using System.Collections.Generic;

namespace ExactTarget.TriggeredEmail.Core.RequestClients.Email
{
    public interface IEmailRequestClient : IDisposable
    {
        int CreateEmailFromTemplate(int emailTemplateId, string emailName, string subject, KeyValuePair<string, string> contentArea);
        int CreateEmail(string externalKey, string emailName, string subject, string htmlBody);
    }
}