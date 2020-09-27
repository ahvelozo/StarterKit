﻿using App.UI.Mvc5.Infrastructure;
using Domain.Core;
using FluentValidation;

namespace App.UI.Mvc5.Areas.Users.Models
{
	public class AccountPasswordRecoverViewModelValidator : AbstractValidator<AccountPasswordRecoverViewModel>
	{
		public AccountPasswordRecoverViewModelValidator()
		{
			// Email
			RuleFor(model => model.Email).NotEmpty();
			RuleFor(model => model.Email).Matches(_RegularExpressions.SimpleEmailPattern);
			RuleFor(model => model.Email).Length(0, _Constants.UsersEmailMaxLength).WithMessage(GlobalizationManager.GetLocalizedString<AreaResources>("_Validation_MaxLength"));
		}
	}
}
