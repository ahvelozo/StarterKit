﻿using App.UI.Mvc5.Infrastructure;
using App.UI.Mvc5.Models;

namespace App.UI.Mvc5.Areas.Users.Models
{
	public class AccountPasswordRecoverViewModel : BaseViewModel
	{
		[LocalizedDisplayName("Email", ResourceType = typeof(AreaResources))]
		public string Email { get; set; }
	}
}
