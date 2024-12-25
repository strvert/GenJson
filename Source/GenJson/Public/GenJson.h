#pragma once

#include "Modules/ModuleManager.h"

class FGenJsonModule : public IModuleInterface
{
	DECLARE_MULTICAST_DELEGATE(FOnGenJsonModuleStartup);

public:
	virtual void StartupModule() override;
	virtual void ShutdownModule() override;

	FOnGenJsonModuleStartup& OnGenJsonModuleStartupDelegate() { return OnGenJsonModuleStartup; }

private:
	FOnGenJsonModuleStartup OnGenJsonModuleStartup;
};
