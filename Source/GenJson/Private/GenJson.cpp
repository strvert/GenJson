#include "GenJson.h"

#define LOCTEXT_NAMESPACE "FGenJsonModule"

void FGenJsonModule::StartupModule()
{
	OnGenJsonModuleStartup.Broadcast();
}

void FGenJsonModule::ShutdownModule()
{
}

#undef LOCTEXT_NAMESPACE
	
IMPLEMENT_MODULE(FGenJsonModule, GenJson)