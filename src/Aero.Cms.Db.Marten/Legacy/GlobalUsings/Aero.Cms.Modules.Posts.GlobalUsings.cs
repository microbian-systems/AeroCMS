// Global using directives

global using System;
global using System.Collections.Generic;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Linq;
global using Marten;
global using Aero.Core;
global using Aero.Core.Railway;
global using Aero.Cms.Abstractions.Http.Clients;
global using Aero.Cms.Abstractions.Requests;
global using Aero.Cms.Core.Entities;

// Disambiguate Orleans request types from HTTP client types of the same name
global using CreateTagRequest = Aero.Cms.Abstractions.Requests.CreateTagRequest;
global using UpdateTagRequest = Aero.Cms.Abstractions.Requests.UpdateTagRequest;
global using DeleteTagRequest = Aero.Cms.Abstractions.Requests.DeleteTagRequest;
global using CreateCategoryRequest = Aero.Cms.Abstractions.Requests.CreateCategoryRequest;
global using UpdateCategoryRequest = Aero.Cms.Abstractions.Requests.UpdateCategoryRequest;
global using DeleteCategoryRequest = Aero.Cms.Abstractions.Requests.DeleteCategoryRequest;
global using Aero.Cms.Modules.Posts.Models;
global using Aero.Cms.Modules.Pages;
global using FluentValidation;
global using Marten;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Logging;