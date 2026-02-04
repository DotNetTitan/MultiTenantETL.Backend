<template>
  <v-card class="wizard-card" elevation="0">
    <v-stepper v-model="currentStep" alt-labels flat class="wizard-stepper">
      <v-stepper-header>
        <v-stepper-item
          :complete="currentStep > 1"
          :value="1"
          :title="t('common.basicInfo')"
          :subtitle="t('common.typeAndProvider')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :complete="currentStep > 2"
          :value="2"
          :title="t('common.connection')"
          :subtitle="t('common.connectionDetails')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :complete="currentStep > 3"
          :value="3"
          :title="t('common.schema')"
          :subtitle="t('common.defineDataStructure')"
        />
        <v-icon v-if="showWriteConfigStep" class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          v-if="showWriteConfigStep"
          :complete="currentStep > 4"
          :value="4"
          :title="t('common.writeConfig')"
          :subtitle="t('common.destinationSettings')"
        />
        <v-icon class="step-arrow">mdi-chevron-right</v-icon>
        <v-stepper-item
          :value="showWriteConfigStep ? 5 : 4"
          :title="t('common.reviewAndSave')"
          :subtitle="t('common.reviewConfiguration')"
        />
      </v-stepper-header>

      <v-stepper-window class="stepper-window">
        <!-- Step 1: Basic Info -->
        <v-stepper-window-item :value="1">
          <div class="pa-6">
            <div class="text-h5 mb-4">{{ t('connectors.connectorName') }}</div>
            <v-row>
              <v-col cols="12" md="8">
                <v-text-field
                  v-model="connector.name"
                  :label="t('connectors.connectorName')"
                  :placeholder="t('connectors.namePlaceholder')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: t('common.name') })]"
                  required
                />
              </v-col>
              <v-col cols="12">
                <v-textarea
                  v-model="connector.description"
                  :label="t('common.description')"
                  :placeholder="t('connectors.descriptionPlaceholder')"
                  variant="outlined"
                  rows="2"
                  auto-grow
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-select
                  v-model="connector.type"
                  :items="connectorTypes"
                  item-title="title"
                  item-value="value"
                  :label="t('connectors.type')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: t('connectors.type') })]"
                  required
                  @update:model-value="handleTypeChange"
                >
                  <template #item="{ item, props }">
                    <v-list-item v-bind="props">
                      <template #prepend>
                        <v-icon>{{ getTypeIcon(item.value) }}</v-icon>
                      </template>
                    </v-list-item>
                  </template>
                </v-select>
              </v-col>
              <v-col cols="12" md="4">
                <v-select
                  v-model="connector.provider"
                  :items="providerOptions"
                  :label="t('connectors.provider')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: t('connectors.provider') })]"
                  required
                  :disabled="!connector.type"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-select
                  v-model="connector.direction"
                  :items="directionOptions"
                  item-title="title"
                  item-value="value"
                  :label="t('connectors.direction')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: t('connectors.direction') })]"
                  required
                  :hint="t('common.howConnectorUsed')"
                  persistent-hint
                >
                  <template #item="{ item, props }">
                    <v-list-item v-bind="props">
                      <template #prepend>
                        <v-icon>{{ item.raw.icon }}</v-icon>
                      </template>
                    </v-list-item>
                  </template>
                </v-select>
              </v-col>
            </v-row>
          </div>
        </v-stepper-window-item>

        <!-- Step 2: Connection Details -->
        <v-stepper-window-item :value="2">
          <div class="pa-6">
            <!-- Context Summary -->
            <div class="text-caption text-medium-emphasis mb-2">
              {{ $t('common.configuring') }}: <span class="font-weight-medium">{{ connector.type }}</span> · 
              <span class="font-weight-medium">{{ connector.provider }}</span> · 
              <span class="font-weight-medium">{{ connector.direction === 'source' ? $t('connectors.source') : connector.direction === 'destination' ? $t('connectors.destination') : $t('connectors.sourceAndDestination') }}</span>
            </div>
            
            <div class="text-h5 mb-4">{{ t('common.connectionConfiguration') }}</div>
            
            <!-- Database Connection -->
            <v-row v-if="connector.type === 'Database'">
              <!-- Snowflake Configuration -->
              <template v-if="connector.provider === 'Snowflake'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.account"
                    :label="t('connectors.snowflakeAccount')"
                    :placeholder="t('connectors.snowflakeAccountPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.snowflakeAccount') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.warehouse"
                    :label="t('connectors.snowflakeWarehouse')"
                    :placeholder="t('connectors.snowflakeWarehousePlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.snowflakeWarehouse') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.database"
                    :label="t('connectors.databaseName')"
                    :placeholder="t('connectors.snowflakeDatabasePlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.databaseName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.schema"
                    :label="t('connectors.snowflakeSchema')"
                    :placeholder="t('connectors.snowflakeSchemaPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.snowflakeSchema') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.username"
                    :label="t('connectors.username')"
                    :placeholder="t('connectors.snowflakeUsernamePlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.username') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.password"
                    :label="t('connectors.password')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.password') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.role"
                    :label="t('connectors.snowflakeRole')"
                    :placeholder="t('connectors.snowflakeRolePlaceholder')"
                    variant="outlined"
                    :hint="t('connectors.snowflakeRoleHint')"
                  />
                </v-col>
              </template>
              
              <!-- BigQuery Provider -->
              <template v-else-if="connector.provider === 'BigQuery'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.projectId"
                    :label="t('connectors.bigQueryProjectId')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.bigQueryProjectId') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.datasetId"
                    :label="t('connectors.bigQueryDatasetId')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.bigQueryDatasetId') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.location"
                    :label="t('connectors.bigQueryLocation')"
                    variant="outlined"
                  />
                </v-col>
                <v-col cols="12">
                  <v-textarea
                    v-model="connector.config.jsonCredentials"
                    :label="t('connectors.bigQueryJsonCredentials')"
                    variant="outlined"
                    rows="4"
                    :rules="[v => !v || isValidJson(v) || t('validation.invalidJson')]"
                  />
                </v-col>
              </template>
              
              <!-- MongoDB Provider -->
              <template v-else-if="connector.provider === 'MongoDb'">
                <v-col cols="12">
                  <v-textarea
                    v-model="connector.config.connectionString"
                    :label="t('connectors.connectionString')"
                    :placeholder="t('connectors.mongodbConnectionStringPlaceholder')"
                    variant="outlined"
                    rows="2"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.connectionString') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.database"
                    :label="t('connectors.databaseName')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.databaseName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.collectionName"
                    :label="t('connectors.collectionName')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.collectionName') })]"
                    required
                  />
                </v-col>
                <v-col v-if="connector.direction === 'source' || connector.direction === 'both'" cols="12">
                  <v-textarea
                    v-model="connector.config.filterJson"
                    :label="t('connectors.filterJson')"
                    :placeholder="t('connectors.filterJsonPlaceholder')"
                    variant="outlined"
                    rows="3"
                    :rules="[v => !v || isValidJson(v) || t('validation.invalidJson')]"
                  />
                </v-col>
              </template>
              
              <!-- Cosmos DB Provider -->
              <template v-else-if="connector.provider === 'CosmosDb'">
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.cosmosEndpoint"
                    :label="t('connectors.cosmosEndpoint')"
                    :placeholder="t('connectors.cosmosEndpointPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.cosmosEndpoint') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.cosmosKey"
                    :label="t('connectors.cosmosKey')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.cosmosKey') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.database"
                    :label="t('connectors.databaseName')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.databaseName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.container"
                    :label="t('connectors.containerName')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.containerName') })]"
                    required
                  />
                </v-col>
                <v-col v-if="connector.direction === 'source' || connector.direction === 'both'" cols="12">
                  <v-textarea
                    v-model="connector.config.query"
                    :label="t('connectors.customQuery')"
                    placeholder="SELECT * FROM c"
                    variant="outlined"
                    rows="3"
                  />
                </v-col>
              </template>
              
              <!-- Other Database Providers (SQL Server, PostgreSQL, MySQL) -->
              <template v-else>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.host"
                    :label="t('connectors.server')"
                    :placeholder="t('connectors.serverPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.server') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model.number="connector.config.port"
                    :label="t('connectors.port')"
                    :placeholder="getDefaultPort(connector.provider).toString()"
                    variant="outlined"
                    type="number"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.database"
                    :label="t('connectors.databaseName')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.databaseName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.username"
                    :label="t('connectors.username')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.username') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.password"
                    :label="t('connectors.password')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.password') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-switch
                    v-model="connector.config.useSsl"
                    :label="t('connectors.useSsl')"
                    color="primary"
                    hide-details
                  />
                </v-col>
              </template>
              
              <!-- Source/Both: Table Name or Query (Excluded for MongoDB and CosmosDB) -->
              <template v-if="(connector.direction === 'source' || connector.direction === 'both') && connector.provider !== 'MongoDb' && connector.provider !== 'CosmosDb'">
                <v-col cols="12">
                  <v-switch
                    v-model="connector.config.useCustomQuery"
                    :label="t('connectors.useCustomQuery')"
                    color="primary"
                    hide-details
                    :hint="t('connectors.useCustomQueryHint')"
                  />
                </v-col>
                <v-col v-if="!connector.config.useCustomQuery" cols="12">
                  <v-text-field
                    v-model="connector.config.tableName"
                    :label="t('connectors.tableName')"
                    :placeholder="t('connectors.tableNamePlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.tableName') })]"
                    required
                  />
                </v-col>
                <v-col v-else cols="12">
                  <v-textarea
                    v-model="connector.config.query"
                    :label="t('connectors.customQuery')"
                    :placeholder="t('connectors.customQueryPlaceholder')"
                    variant="outlined"
                    rows="4"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.customQuery') })]"
                    required
                  />
                </v-col>
              </template>
              
              <v-col cols="12">
                <v-switch
                  v-model="connector.config.useCustomConnectionString"
                  :label="t('connectors.useCustomConnectionString')"
                  color="primary"
                  hide-details
                />
              </v-col>
              <v-col v-if="connector.config.useCustomConnectionString" cols="12">
                <v-textarea
                  v-model="connector.config.connectionString"
                  :label="t('connectors.connectionString')"
                  variant="outlined"
                  rows="3"
                  :rules="[v => !!v || t('validation.required', { field: t('connectors.connectionString') })]"
                />
              </v-col>

              <!-- Test Connection Button -->
              <v-col cols="12" class="text-center mt-4">
                <v-btn
                  color="success"
                  variant="outlined"
                  prepend-icon="mdi-connection"
                  :loading="testingConnection"
                  :disabled="!validateConnectionConfig()"
                  @click="testConnectionBeforeSave"
                >
                  {{ t('connectors.testConnection') }}
                </v-btn>
                <div v-if="connectionTestResult" class="mt-3">
                  <v-alert
                    :type="connectionTestSuccess ? 'success' : 'error'"
                    variant="tonal"
                    density="compact"
                  >
                    {{ connectionTestMessage }}
                  </v-alert>
                </div>
              </v-col>
            </v-row>

            <!-- API Connection -->
            <v-row v-else-if="connector.type === 'API'">
              <v-col cols="12">
                <v-text-field
                  v-model="connector.config.baseUrl"
                  :label="t('connectors.apiUrl')"
                  :placeholder="t('connectors.apiUrlPlaceholder')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: 'URL' })]"
                  required
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="connector.config.authType"
                  :items="authTypes"
                  :label="t('connectors.authenticationType')"
                  variant="outlined"
                />
              </v-col>
              <!-- Bearer Token Configuration -->
              <template v-if="connector.config.authType === 'Bearer'">
                <v-col cols="12">
                  <v-switch
                    v-model="connector.config.useDynamicToken"
                    :label="t('connectors.useDynamicToken')"
                    color="primary"
                    density="compact"
                    hide-details
                    :hint="t('connectors.useDynamicTokenHint')"
                  />
                </v-col>
                
                <!-- Static Token -->
                <v-col v-if="!connector.config.useDynamicToken" cols="12">
                  <v-text-field
                    v-model="connector.config.authToken"
                    :label="t('connectors.bearerToken')"
                    type="password"
                    variant="outlined"
                    :hint="t('connectors.staticTokenHint')"
                    persistent-hint
                  />
                </v-col>
                
                <!-- Dynamic Token Configuration -->
                <template v-else>
                  <v-col cols="12">
                    <v-text-field
                      v-model="connector.config.tokenEndpointUrl"
                      :label="t('connectors.tokenEndpointUrl')"
                      placeholder="https://api.example.com/auth/token"
                      variant="outlined"
                      :rules="[v => !!v || t('validation.required', { field: t('connectors.tokenEndpointUrl') })]"
                      required
                    />
                  </v-col>
                  
                  <v-col cols="12" md="6">
                    <v-select
                      v-model="connector.config.tokenEndpointMethod"
                      :items="['POST', 'GET']"
                      :label="t('connectors.httpMethod')"
                      variant="outlined"
                    />
                  </v-col>
                  
                  <v-col cols="12" md="6">
                    <v-text-field
                      v-model="connector.config.tokenResponsePath"
                      :label="t('connectors.tokenResponsePath')"
                      placeholder="access_token"
                      variant="outlined"
                      :hint="t('connectors.tokenResponsePathHint')"
                      persistent-hint
                    />
                  </v-col>
                  
                  <v-col cols="12">
                    <v-textarea
                      v-model="connector.config.tokenEndpointBody"
                      :label="t('connectors.requestBody')"
                      placeholder="{&quot;client_id&quot;: &quot;xxx&quot;, &quot;client_secret&quot;: &quot;yyy&quot;, &quot;grant_type&quot;: &quot;client_credentials&quot;}"
                      variant="outlined"
                      rows="3"
                      :hint="t('connectors.tokenRequestBodyHint')"
                      persistent-hint
                    />
                  </v-col>
                  
                  <v-col cols="12">
                    <v-textarea
                      v-model="connector.config.tokenEndpointHeadersText"
                      :label="t('connectors.requestHeaders')"
                      placeholder="{&quot;Content-Type&quot;: &quot;application/json&quot;}"
                      variant="outlined"
                      rows="2"
                      :hint="t('connectors.tokenRequestHeadersHint')"
                      persistent-hint
                    />
                  </v-col>
                  
                  <v-col cols="12" md="6">
                    <v-text-field
                      v-model.number="connector.config.tokenExpirySeconds"
                      :label="t('connectors.tokenCacheDuration')"
                      type="number"
                      placeholder="3600"
                      variant="outlined"
                      :hint="t('connectors.tokenCacheDurationHint')"
                      persistent-hint
                    />
                  </v-col>
                </template>
              </template>
              <v-col v-if="connector.config.authType === 'API Key'" cols="12" md="6">
                <v-text-field
                  v-model="connector.config.apiKeyHeader"
                  :label="t('connectors.apiKeyHeader')"
                  :placeholder="t('connectors.apiKeyHeaderPlaceholder')"
                  variant="outlined"
                  :hint="t('connectors.apiKeyHeaderHint')"
                  persistent-hint
                />
              </v-col>
              <v-col v-if="connector.config.authType === 'API Key'" cols="12" md="6">
                <v-text-field
                  v-model="connector.config.apiKeyValue"
                  :label="t('connectors.apiKeyValue')"
                  type="password"
                  variant="outlined"
                />
              </v-col>
              <v-col v-if="connector.config.authType === 'Basic'" cols="12" md="6">
                <v-text-field
                  v-model="connector.config.username"
                  :label="t('connectors.username')"
                  variant="outlined"
                />
              </v-col>
              <v-col v-if="connector.config.authType === 'Basic'" cols="12" md="6">
                <v-text-field
                  v-model="connector.config.password"
                  :label="t('connectors.password')"
                  type="password"
                  variant="outlined"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model.number="connector.config.timeoutSeconds"
                  :label="t('connectors.timeout')"
                  type="number"
                  variant="outlined"
                  :hint="t('connectors.timeoutHint')"
                  persistent-hint
                />
              </v-col>

              <!-- Custom Headers -->
              <v-col cols="12">
                <v-textarea
                  v-model="customHeadersText"
                  :label="t('connectors.customHeaders')"
                  :placeholder="t('connectors.customHeadersPlaceholder')"
                  variant="outlined"
                  rows="3"
                  :hint="t('connectors.customHeadersHint')"
                  persistent-hint
                />
              </v-col>

              <!-- API Endpoints Configuration -->
              <v-col cols="12">
                <ApiEndpointEditor
                  v-model="connector.config.endpoints"
                  :direction="connector.direction"
                />
              </v-col>

              <!-- Test Connection Button -->
              <v-col cols="12" class="text-center mt-4">
                <v-btn
                  color="success"
                  variant="outlined"
                  prepend-icon="mdi-connection"
                  :loading="testingConnection"
                  :disabled="!validateConnectionConfig()"
                  @click="testConnectionBeforeSave"
                >
                  {{ t('connectors.testConnection') }}
                </v-btn>
                <div v-if="connectionTestResult" class="mt-3">
                  <v-alert
                    :type="connectionTestSuccess ? 'success' : 'error'"
                    variant="tonal"
                    density="compact"
                  >
                    {{ connectionTestMessage }}
                  </v-alert>
                </div>
              </v-col>
            </v-row>

            <!-- File Connection -->
            <v-row v-else-if="connector.type === 'File'">
              <v-col cols="12" md="6">
                <v-select
                  v-model="connector.config.format"
                  :items="fileFormats"
                  :label="t('connectors.fileFormat')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: t('connectors.fileFormat') })]"
                  required
                />
              </v-col>

              <!-- Local Storage -->
              <template v-if="connector.provider === 'Local'">
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.path"
                    :label="t('connectors.filePath')"
                    :placeholder="t('connectors.filePathPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.filePath') })]"
                    required
                  />
                </v-col>
              </template>

              <!-- FTP Storage -->
              <template v-if="connector.provider === 'FTP'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.ftpHost"
                    :label="t('connectors.ftpHost')"
                    :placeholder="t('connectors.ftpHostPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: 'Host' })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model.number="connector.config.ftpPort"
                    :label="t('connectors.port')"
                    placeholder="21"
                    type="number"
                    variant="outlined"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.ftpUsername"
                    :label="t('connectors.username')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.username') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.ftpPassword"
                    :label="t('connectors.password')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.password') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.path"
                    :label="t('connectors.filePath')"
                    :placeholder="t('connectors.remotePathPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.filePath') })]"
                    required
                  />
                </v-col>
              </template>

              <!-- SFTP Storage -->
              <template v-if="connector.provider === 'SFTP'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.sftpHost"
                    :label="t('connectors.sftpHost')"
                    :placeholder="t('connectors.sftpHostPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: 'Host' })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model.number="connector.config.sftpPort"
                    :label="t('connectors.port')"
                    placeholder="22"
                    type="number"
                    variant="outlined"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.sftpUsername"
                    :label="t('connectors.username')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.username') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.sftpPassword"
                    :label="t('connectors.password')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.password') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.path"
                    :label="t('connectors.filePath')"
                    :placeholder="t('connectors.remotePathPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.filePath') })]"
                    required
                  />
                </v-col>
              </template>

              <!-- S3 Storage -->
              <template v-if="connector.provider === 'S3'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.s3Bucket"
                    :label="t('connectors.bucketName')"
                    :placeholder="t('connectors.bucketPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.bucketName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.s3Region"
                    :label="t('connectors.region')"
                    :placeholder="t('connectors.regionPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.region') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.s3AccessKey"
                    :label="t('connectors.accessKeyId')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.accessKeyId') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.s3SecretKey"
                    :label="t('connectors.secretAccessKey')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.secretAccessKey') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.s3Endpoint"
                    :label="t('connectors.s3Endpoint')"
                    :placeholder="t('connectors.s3EndpointPlaceholder')"
                    variant="outlined"
                    :hint="t('connectors.s3EndpointHint')"
                    persistent-hint
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.path"
                    :label="t('connectors.objectKey')"
                    :placeholder="t('connectors.objectKeyPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.objectKey') })]"
                    required
                  />
                </v-col>
              </template>

              <!-- AzureBlob Storage -->
              <template v-if="connector.provider === 'AzureBlob'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.azureAccountName"
                    :label="t('connectors.storageAccountName')"
                    :placeholder="t('connectors.storageAccountPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.storageAccountName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.azureContainer"
                    :label="t('connectors.containerName')"
                    :placeholder="t('connectors.containerPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.containerName') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.azureAccountKey"
                    :label="t('connectors.accountKey')"
                    type="password"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.accountKey') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.path"
                    :label="t('connectors.blobPath')"
                    :placeholder="t('connectors.blobPathPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.blobPath') })]"
                    required
                  />
                </v-col>
              </template>
              
              <!-- GCS Storage -->
              <template v-if="connector.provider === 'GCS'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.gcsBucket"
                    :label="t('connectors.gcsBucket')"
                    :placeholder="t('connectors.gcsBucketPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.gcsBucket') })]"
                    required
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.gcsProjectId"
                    :label="t('connectors.gcsProjectId')"
                    :placeholder="t('connectors.gcsProjectIdPlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.gcsProjectId') })]"
                    required
                  />
                </v-col>
                <v-col cols="12">
                  <v-textarea
                    v-model="connector.config.gcsJsonCredentials"
                    :label="t('connectors.gcsJsonCredentials')"
                    variant="outlined"
                    rows="4"
                    :rules="[v => !v || isValidJson(v) || t('validation.invalidJson')]"
                  />
                </v-col>
                <v-col cols="12">
                  <v-text-field
                    v-model="connector.config.path"
                    :label="t('connectors.filePath')"
                    placeholder="e.g., path/to/file.csv"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.filePath') })]"
                    required
                  />
                </v-col>
              </template>

              <!-- Format-specific options (CSV) -->
              <v-col v-if="connector.config.format === 'CSV'" cols="12" md="6">
                <v-text-field
                  v-model="connector.config.delimiter"
                  :label="t('common.delimiter')"
                  placeholder=","
                  variant="outlined"
                />
              </v-col>
              <v-col v-if="connector.config.format === 'CSV'" cols="12" md="6">
                <v-switch
                  v-model="connector.config.hasHeader"
                  :label="t('common.hasHeaderRow')"
                  color="primary"
                  hide-details
                />
              </v-col>

              <!-- Test Connection Button -->
              <v-col cols="12" class="text-center mt-4">
                <v-btn
                  color="success"
                  variant="outlined"
                  prepend-icon="mdi-connection"
                  :loading="testingConnection"
                  :disabled="!validateConnectionConfig()"
                  @click="testConnectionBeforeSave"
                >
                  {{ t('connectors.testConnection') }}
                </v-btn>
                <div v-if="connectionTestResult" class="mt-3">
                  <v-alert
                    :type="connectionTestSuccess ? 'success' : 'error'"
                    variant="tonal"
                    density="compact"
                  >
                    {{ connectionTestMessage }}
                  </v-alert>
                </div>
              </v-col>
            </v-row>
          </div>
        </v-stepper-window-item>

        <!-- Step 3: Schema Definition -->
        <v-stepper-window-item :value="3">
          <div class="pa-6">
            <!-- Context Summary -->
            <div class="text-caption text-medium-emphasis mb-2">
              {{ $t('common.configuring') }}: <span class="font-weight-medium">{{ connector.type }}</span> · 
              <span class="font-weight-medium">{{ connector.provider }}</span><template v-if="connector.type === 'File'">
                · 
                <span class="font-weight-medium">{{ connector.config.format }}</span>
              </template> · 
              <span class="font-weight-medium">{{ connector.direction === 'source' ? $t('connectors.source') : connector.direction === 'destination' ? $t('connectors.destination') : $t('connectors.sourceAndDestination') }}</span>
            </div>
            
            <div class="text-h5 mb-4">{{ t('connectors.schemaDefinition') }}</div>
            
            <SchemaEditor
              v-model="connector.schema.fields"
              :connector-type="connector.type"
              :connector-id="connector.id"
              :provider="connector.provider"
              :config="connector.config"
              @validate="handleSchemaValidation"
            />
          </div>
        </v-stepper-window-item>

        <!-- Step 4: Write Configuration (only for destinations) -->
        <v-stepper-window-item v-if="showWriteConfigStep" :value="4">
          <div class="pa-6">
            <!-- Context Summary -->
            <div class="text-caption text-medium-emphasis mb-2">
              {{ $t('common.configuring') }}: <span class="font-weight-medium">{{ connector.type }}</span> · 
              <span class="font-weight-medium">{{ connector.provider }}</span><template v-if="connector.type === 'File'">
                · 
                <span class="font-weight-medium">{{ connector.config.format }}</span>
              </template> · 
              <span class="font-weight-medium">{{ connector.direction === 'source' ? $t('connectors.source') : connector.direction === 'destination' ? $t('connectors.destination') : $t('connectors.sourceAndDestination') }}</span> · 
              <span class="font-weight-medium">{{ connector.schema.fields.length }} {{ $t('connectors.fields', connector.schema.fields.length) }}</span>
            </div>
            
            <div class="text-h5 mb-4">{{ $t('common.writeConfiguration') }}</div>
            
            <p class="text-body-2 text-medium-emphasis mb-6">
              {{ $t('connectors.writeConfigDescription') }}
            </p>

            <!-- Database Write Config -->
            <v-row v-if="connector.type === 'Database'">
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="connector.config.writeConfig.tableName"
                  :label="t('connectors.tableName')"
                  :placeholder="t('connectors.tableNamePlaceholder')"
                  variant="outlined"
                  :rules="[v => !!v || t('validation.required', { field: t('connectors.tableName') })]"
                  required
                  :hint="t('connectors.tableNameHint')"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="connector.config.writeConfig.operation"
                  :items="['INSERT', 'UPDATE', 'UPSERT', 'BULK_INSERT']"
                  :label="t('connectors.writeOperation')"
                  variant="outlined"
                  :hint="t('connectors.writeOperationHint')"
                  persistent-hint
                >
                  <template #item="{ item, props }">
                    <v-list-item v-bind="props">
                      <template #subtitle>
                        <span v-if="item.value === 'INSERT'">{{ $t('connectors.insertDescription') }}</span>
                        <span v-else-if="item.value === 'UPDATE'">{{ $t('connectors.updateDescription') }}</span>
                        <span v-else-if="item.value === 'UPSERT'">{{ $t('connectors.upsertDescription') }}</span>
                        <span v-else-if="item.value === 'BULK_INSERT'">{{ $t('connectors.bulkInsertDescription') }}</span>
                      </template>
                    </v-list-item>
                  </template>
                </v-select>
              </v-col>
              <v-col v-if="connector.config.writeConfig && ['UPDATE', 'UPSERT'].includes(connector.config.writeConfig.operation)" cols="12">
                <v-combobox
                  v-model="connector.config.writeConfig.primaryKeys"
                  :items="connector.schema.fields.map(f => f.name)"
                  :label="t('connectors.primaryKeyFields')"
                  variant="outlined"
                  multiple
                  chips
                  closable-chips
                  :rules="[v => (v && v.length > 0) || t('connectors.primaryKeyRequired')]"
                  :hint="t('connectors.primaryKeyHint')"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model.number="connector.config.writeConfig.batchSize"
                  :label="t('connectors.batchSize')"
                  type="number"
                  variant="outlined"
                  :hint="t('connectors.batchSizeHint')"
                  persistent-hint
                  :min="1"
                  :max="10000"
                />
              </v-col>
            </v-row>

            <!-- API Write Config -->
            <v-row v-else-if="connector.type === 'API'">
              <v-col cols="12" md="6">
                <v-select
                  v-model="connector.config.writeConfig.requestFormat"
                  :items="['JSON', 'XML', 'Form Data']"
                  :label="t('connectors.requestFormat')"
                  variant="outlined"
                  :hint="t('connectors.requestFormatHint')"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-switch
                  v-model="connector.config.writeConfig.wrapInArray"
                  :label="t('connectors.wrapDataInArray')"
                  color="primary"
                  :hint="t('connectors.wrapDataHint')"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="connector.config.writeConfig.rootKey"
                  :label="t('connectors.rootKeyOptional')"
                  :placeholder="t('connectors.rootKeyPlaceholder')"
                  variant="outlined"
                  :hint="t('connectors.rootKeyHint')"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model.number="connector.config.writeConfig.batchSize"
                  :label="t('connectors.batchSize')"
                  type="number"
                  variant="outlined"
                  :hint="t('connectors.recordsPerRequest')"
                  persistent-hint
                  :min="1"
                  :max="1000"
                />
              </v-col>
            </v-row>

            <!-- File Write Config -->
            <v-row v-else-if="connector.type === 'File'">
              <v-col cols="12">
                <v-text-field
                  v-model="connector.config.writeConfig.filenamePattern"
                  :label="t('connectors.filenamePattern')"
                  variant="outlined"
                  :placeholder="t('connectors.filenamePatternPlaceholder')"
                  :hint="t('connectors.filenamePatternHint', { format: connector.config.format })"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12">
                <div class="text-caption text-medium-emphasis mb-2">
                  {{ $t('connectors.availablePlaceholders') }}:
                </div>
                <v-tooltip :text="$t('connectors.dateFormatTooltip')" location="top">
                  <template #activator="{ props }">
                    <v-chip size="small" class="mr-2 mb-2" v-bind="props">{date}</v-chip>
                  </template>
                </v-tooltip>
                <v-tooltip :text="$t('connectors.timeFormatTooltip')" location="top">
                  <template #activator="{ props }">
                    <v-chip size="small" class="mr-2 mb-2" v-bind="props">{time}</v-chip>
                  </template>
                </v-tooltip>
                <v-tooltip :text="$t('connectors.timestampTooltip')" location="top">
                  <template #activator="{ props }">
                    <v-chip size="small" class="mr-2 mb-2" v-bind="props">{timestamp}</v-chip>
                  </template>
                </v-tooltip>
                <v-tooltip :text="$t('connectors.pipelineTooltip')" location="top">
                  <template #activator="{ props }">
                    <v-chip size="small" class="mr-2 mb-2" v-bind="props">{pipeline}</v-chip>
                  </template>
                </v-tooltip>
                <v-tooltip :text="$t('connectors.executionIdTooltip')" location="top">
                  <template #activator="{ props }">
                    <v-chip size="small" class="mr-2 mb-2" v-bind="props">{executionId}</v-chip>
                  </template>
                </v-tooltip>
                <div class="text-caption text-medium-emphasis mt-2">
                  {{ $t('connectors.filenameExample') }}
                </div>
              </v-col>

              <!-- CSV Specific -->
              <template v-if="connector.config.format === 'CSV'">
                <v-col cols="12" md="6">
                  <v-switch
                    v-model="connector.config.writeConfig.includeHeaders"
                    :label="t('connectors.includeHeaderRow')"
                    color="primary"
                    :hint="t('connectors.includeHeaderHint')"
                    persistent-hint
                  />
                </v-col>
                <v-col cols="12">
                  <v-combobox
                    v-model="connector.config.writeConfig.columnOrder"
                    :items="connector.schema.fields.map(f => f.name)"
                    :label="t('connectors.columnOrder')"
                    variant="outlined"
                    multiple
                    chips
                    closable-chips
                    :hint="t('connectors.columnOrderHint')"
                    persistent-hint
                  />
                </v-col>
              </template>

              <!-- Excel Specific -->
              <template v-if="connector.config.format === 'Excel'">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.writeConfig.sheetName"
                    :label="t('connectors.sheetName')"
                    :placeholder="t('connectors.sheetNamePlaceholder')"
                    variant="outlined"
                    :rules="[v => !!v || t('validation.required', { field: t('connectors.sheetName') })]"
                    :hint="t('connectors.sheetNameHint')"
                    persistent-hint
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.writeConfig.startCell"
                    :label="t('connectors.startCell')"
                    :placeholder="t('connectors.startCellPlaceholder')"
                    variant="outlined"
                    :hint="t('connectors.startCellHint')"
                    persistent-hint
                  />
                </v-col>
              </template>

              <!-- JSON Specific -->
              <template v-if="connector.config.format === 'JSON'">
                <v-col cols="12" md="6">
                  <v-select
                    v-model="connector.config.writeConfig.structure"
                    :items="['array', 'object', 'nested']"
                    :label="t('connectors.jsonStructure')"
                    variant="outlined"
                    :hint="t('connectors.jsonStructureHint')"
                    persistent-hint
                  >
                    <template #item="{ item, props }">
                      <v-list-item v-bind="props">
                        <template #subtitle>
                          <span v-if="item.value === 'array'">{{ $t('connectors.arrayOfObjects') }}</span>
                          <span v-else-if="item.value === 'object'">{{ $t('connectors.singleObject') }}</span>
                          <span v-else>{{ $t('connectors.nestedStructure') }}</span>
                        </template>
                      </v-list-item>
                    </template>
                  </v-select>
                </v-col>
                <v-col v-if="connector.config.writeConfig.structure === 'nested'" cols="12" md="6">
                  <v-text-field
                    v-model="connector.config.writeConfig.rootKey"
                    :label="t('connectors.rootKey')"
                    :placeholder="t('connectors.rootKeyPlaceholder')"
                    variant="outlined"
                    :hint="t('connectors.rootKeyNestedHint')"
                    persistent-hint
                  />
                </v-col>
              </template>
            </v-row>
          </div>
        </v-stepper-window-item>

        <!-- Step 4/5: Review & Save -->
        <v-stepper-window-item :value="showWriteConfigStep ? 5 : 4">
          <div class="pa-6">
            <div class="text-h5 mb-4">{{ t('common.reviewConfiguration') }}</div>
            
            <!-- Basic Information -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-information</v-icon>
                {{ t('executions.basicInformation') }}
              </v-card-title>
              <v-card-text>
                <v-list density="compact">
                  <v-list-item>
                    <template #prepend>
                      <v-icon>mdi-label</v-icon>
                    </template>
                    <v-list-item-title>{{ t('common.name') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ connector.name }}</v-list-item-subtitle>
                  </v-list-item>
                  <v-list-item v-if="connector.description">
                    <template #prepend>
                      <v-icon>mdi-text</v-icon>
                    </template>
                    <v-list-item-title>{{ t('common.description') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ connector.description }}</v-list-item-subtitle>
                  </v-list-item>
                  <v-list-item>
                    <template #prepend>
                      <v-icon>{{ getTypeIcon(connector.type) }}</v-icon>
                    </template>
                    <v-list-item-title>{{ t('common.type') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ connector.type }} - {{ connector.provider }}</v-list-item-subtitle>
                  </v-list-item>
                  <v-list-item>
                    <template #prepend>
                      <v-icon>{{ getDirectionIcon(connector.direction) }}</v-icon>
                    </template>
                    <v-list-item-title>{{ t('connectors.direction') }}</v-list-item-title>
                    <v-list-item-subtitle>{{ getDirectionLabel(connector.direction) }}</v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </v-card-text>
            </v-card>

            <!-- Connection Details -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-connection</v-icon>
                {{ t('common.connectionDetails') }}
              </v-card-title>
              <v-card-text>
                <v-list density="compact">
                  <!-- Database Connection Details -->
                  <template v-if="connector.type === 'Database'">
                    <template v-if="connector.provider === 'BigQuery'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-google-cloud</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.bigQueryProjectId') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.projectId }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-database</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.bigQueryDatasetId') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.datasetId }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item v-if="connector.config.location">
                        <template #prepend>
                          <v-icon>mdi-map-marker</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.bigQueryLocation') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.location }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-else-if="connector.provider === 'Snowflake'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-snowflake</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.snowflakeAccount') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.account }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-warehouse</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.snowflakeWarehouse') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.warehouse }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-database</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.databaseName') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.database }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-schema</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.snowflakeSchema') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.schema }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-account</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.username') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.username }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-else-if="connector.provider === 'CosmosDb'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-link-variant</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.cosmosEndpoint') }}</v-list-item-title>
                        <v-list-item-subtitle class="text-truncate">{{ connector.config.cosmosEndpoint }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-database-cog</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.containerName') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.container }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item v-if="connector.config.query">
                        <template #prepend>
                          <v-icon>mdi-code-braces</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.customQuery') }}</v-list-item-title>
                        <v-list-item-subtitle class="text-truncate">{{ connector.config.query }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-else-if="connector.provider === 'MongoDb'">
                      <v-list-item v-if="connector.config.connectionString">
                        <template #prepend>
                          <v-icon>mdi-link-variant</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.connectionString') }}</v-list-item-title>
                        <v-list-item-subtitle class="text-truncate">{{ connector.config.connectionString }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-database-cog</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.collectionName') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.collectionName }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item v-if="connector.config.filterJson">
                        <template #prepend>
                          <v-icon>mdi-filter-variant</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.filterJson') }}</v-list-item-title>
                        <v-list-item-subtitle class="text-truncate">{{ connector.config.filterJson }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-else>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-server</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.server') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.host }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item v-if="connector.config.port">
                        <template #prepend>
                          <v-icon>mdi-network</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.port') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.port }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-database</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.database') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.database }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-account</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.username') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.username }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <v-list-item v-if="(connector.direction === 'source' || connector.direction === 'both') && connector.config.tableName">
                      <template #prepend>
                        <v-icon>mdi-table</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.tableName') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.tableName }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="(connector.direction === 'source' || connector.direction === 'both') && connector.config.query">
                      <template #prepend>
                        <v-icon>mdi-code-braces</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.customQuery') }}</v-list-item-title>
                      <v-list-item-subtitle class="text-truncate">{{ connector.config.query }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.useCustomConnectionString">
                      <template #prepend>
                        <v-icon>mdi-link-variant</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.customConnectionString') }}</v-list-item-title>
                      <v-list-item-subtitle class="text-truncate">{{ connector.config.connectionString }}</v-list-item-subtitle>
                    </v-list-item>
                  </template>

                  <!-- API Connection Details -->
                  <template v-if="connector.type === 'API'">
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-web</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.baseUrl') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.url }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-shield-lock</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.authentication') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.authType }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.headers">
                      <template #prepend>
                        <v-icon>mdi-code-json</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.customHeaders') }}</v-list-item-title>
                      <v-list-item-subtitle class="text-truncate">{{ connector.config.headers }}</v-list-item-subtitle>
                    </v-list-item>
                  </template>

                  <!-- File Connection Details -->
                  <template v-if="connector.type === 'File'">
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-file-document</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.format') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.format }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <template #prepend>
                        <v-icon v-if="connector.provider === 'Local'">mdi-folder</v-icon>
                        <v-icon v-else-if="connector.provider === 'FTP'">mdi-server-network</v-icon>
                        <v-icon v-else-if="connector.provider === 'S3'">mdi-aws</v-icon>
                        <v-icon v-else-if="connector.provider === 'AzureBlob'">mdi-microsoft-azure</v-icon>
                        <v-icon v-else>mdi-cloud</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.storageProvider') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.provider }}</v-list-item-subtitle>
                    </v-list-item>
                    <template v-if="connector.provider === 'FTP'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-server</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.ftpHost') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.ftpHost }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-network</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.port') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.ftpPort || '21' }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-account</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.username') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.ftpUsername }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-if="connector.provider === 'S3'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-bucket</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.s3Bucket') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.s3Bucket }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-earth</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.region') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.s3Region }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-if="connector.provider === 'GCS'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-bucket</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.gcsBucket') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.gcsBucket }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-google-cloud</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.gcsProjectId') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.gcsProjectId }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <template v-if="connector.provider === 'AzureBlob'">
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-table-large</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.storageAccount') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.azureAccountName }}</v-list-item-subtitle>
                      </v-list-item>
                      <v-list-item>
                        <template #prepend>
                          <v-icon>mdi-package-variant</v-icon>
                        </template>
                        <v-list-item-title>{{ t('connectors.container') }}</v-list-item-title>
                        <v-list-item-subtitle>{{ connector.config.azureContainer }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-folder</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.path') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.path }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'CSV'">
                      <template #prepend>
                        <v-icon>mdi-table-split-cell</v-icon>
                      </template>
                      <v-list-item-title>{{ t('common.delimiter') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.delimiter || ',' }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'CSV'">
                      <template #prepend>
                        <v-icon>mdi-format-header-1</v-icon>
                      </template>
                      <v-list-item-title>{{ t('common.hasHeaderRow') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.hasHeader ? t('common.yes') : t('common.no') }}</v-list-item-subtitle>
                    </v-list-item>
                  </template>
                </v-list>
              </v-card-text>
            </v-card>

            <!-- API Endpoints (if applicable) -->
            <v-card v-if="connector.type === 'API' && connector.config.endpoints && connector.config.endpoints.length > 0" variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-api</v-icon>
                {{ t('connectors.apiEndpoints') }} ({{ connector.config.endpoints.length }})
              </v-card-title>
              <v-card-text>
                <v-list density="compact">
                  <v-list-item v-for="endpoint in connector.config.endpoints" :key="endpoint.id">
                    <template #prepend>
                      <v-chip :color="getMethodColor(endpoint.method)" size="small">
                        {{ endpoint.method }}
                      </v-chip>
                    </template>
                    <v-list-item-title>{{ endpoint.path }}</v-list-item-title>
                  </v-list-item>
                </v-list>
              </v-card-text>
            </v-card>

            <!-- Schema Fields -->
            <v-card variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-table</v-icon>
                {{ t('connectors.schemaFields') }} ({{ connector.schema.fields.length }})
              </v-card-title>
              <v-card-text>
                <v-table density="compact">
                  <thead>
                    <tr>
                      <th>{{ t('connectors.fieldName') }}</th>
                      <th>{{ t('connectors.dataType') }}</th>
                      <th>{{ t('common.required') }}</th>
                      <th>{{ t('common.description') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="field in connector.schema.fields" :key="field.id">
                      <td>
                        <v-icon v-if="field.isPrimaryKey" size="small" color="primary" class="mr-1">mdi-key</v-icon>
                        {{ field.name }}
                      </td>
                      <td>
                        <v-chip size="x-small" variant="outlined">{{ field.type }}</v-chip>
                      </td>
                      <td>
                        <v-icon v-if="field.required" size="small" color="error">mdi-check-circle</v-icon>
                        <v-icon v-else size="small" color="grey">mdi-circle-outline</v-icon>
                      </td>
                      <td class="text-caption text-medium-emphasis">{{ field.description || '-' }}</td>
                    </tr>
                  </tbody>
                </v-table>
              </v-card-text>
            </v-card>

            <!-- Write Configuration (if destination) -->
            <v-card v-if="showWriteConfigStep && connector.config.writeConfig" variant="outlined" class="mb-4">
              <v-card-title class="text-subtitle-1 bg-surface-variant">
                <v-icon class="mr-2">mdi-pencil</v-icon>
                {{ t('common.writeConfiguration') }}
              </v-card-title>
              <v-card-text>
                <v-list density="compact">
                  <!-- Database Write Config -->
                  <template v-if="connector.type === 'Database'">
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-table</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.tableName') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.tableName }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-database-edit</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.operation') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.operation }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.writeConfig.primaryKeys && connector.config.writeConfig.primaryKeys.length > 0">
                      <template #prepend>
                        <v-icon>mdi-key</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.primaryKeys') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.primaryKeys.join(', ') }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-package-variant</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.batchSize') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.batchSize }} {{ t('connectors.records') }}</v-list-item-subtitle>
                    </v-list-item>
                  </template>

                  <!-- API Write Config -->
                  <template v-if="connector.type === 'API'">
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-code-json</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.requestFormat') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.requestFormat }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-format-list-bulleted</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.wrapInArray') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.wrapInArray ? t('common.yes') : t('common.no') }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.writeConfig.rootKey">
                      <template #prepend>
                        <v-icon>mdi-key-variant</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.rootKey') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.rootKey }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <template #prepend>
                        <v-icon>mdi-package-variant</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.batchSize') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.batchSize }} {{ t('connectors.records') }}</v-list-item-subtitle>
                    </v-list-item>
                  </template>

                  <!-- File Write Config -->
                  <template v-if="connector.type === 'File'">
                    <v-list-item v-if="connector.config.writeConfig.filenamePattern">
                      <template #prepend>
                        <v-icon>mdi-file-outline</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.filenamePattern') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.filenamePattern }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'CSV'">
                      <template #prepend>
                        <v-icon>mdi-format-header-1</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.includeHeaders') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.includeHeaders ? t('common.yes') : t('common.no') }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'CSV' && connector.config.writeConfig.columnOrder && connector.config.writeConfig.columnOrder.length > 0">
                      <template #prepend>
                        <v-icon>mdi-sort</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.columnOrder') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.columnOrder.join(', ') }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'Excel'">
                      <template #prepend>
                        <v-icon>mdi-file-excel</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.sheetName') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.sheetName }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'Excel'">
                      <template #prepend>
                        <v-icon>mdi-table-large</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.startCell') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.startCell }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'JSON'">
                      <template #prepend>
                        <v-icon>mdi-code-braces</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.jsonStructure') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.structure }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item v-if="connector.config.format === 'JSON' && connector.config.writeConfig.rootKey">
                      <template #prepend>
                        <v-icon>mdi-key-variant</v-icon>
                      </template>
                      <v-list-item-title>{{ t('connectors.rootKey') }}</v-list-item-title>
                      <v-list-item-subtitle>{{ connector.config.writeConfig.rootKey }}</v-list-item-subtitle>
                    </v-list-item>
                  </template>
                </v-list>
              </v-card-text>
            </v-card>
          </div>
        </v-stepper-window-item>
      </v-stepper-window>
    </v-stepper>

    <v-divider />

    <v-card-actions class="pa-4">
      <v-btn
        v-if="currentStep > 1"
        variant="outlined"
        prepend-icon="mdi-chevron-left"
        @click="currentStep--"
      >
        {{ t('common.back') }}
      </v-btn>
      <v-spacer />
      <v-btn
        v-if="currentStep < (showWriteConfigStep ? 5 : 4)"
        color="primary"
        variant="elevated"
        append-icon="mdi-chevron-right"
        :disabled="!canProceed"
        @click="currentStep++"
      >
        {{ t('common.next') }}
      </v-btn>
      <v-btn
        v-else
        color="primary"
        variant="elevated"
        prepend-icon="mdi-content-save"
        :loading="saving"
        :disabled="!canSave"
        @click="handleSave"
      >
        {{ t('common.save') }}
        <v-tooltip activator="parent" location="top">{{ t('common.saveConnector') }}</v-tooltip>
      </v-btn>
    </v-card-actions>
  </v-card>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useTranslatedMetadata } from '@/composables/useTranslatedMetadata';
import SchemaEditor from './SchemaEditor.vue';
import ApiEndpointEditor from './ApiEndpointEditor.vue';

const { t } = useI18n();

const props = defineProps({
  connector: {
    type: Object,
    default: () => ({
      id: null,
      name: '',
      description: '',
      type: '',
      provider: '',
      direction: 'source',
      config: {},
      schema: { fields: [] }
    })
  },
  connectors: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['save', 'close', 'toggle-fullscreen']);

const currentStep = ref(1);
const saving = ref(false);
const schemaValidation = ref({ isValid: true, errors: [] });
const customHeadersText = ref('');
const testingConnection = ref(false);
const connectionTestResult = ref(false);
const connectionTestSuccess = ref(false);
const connectionTestMessage = ref('');

// Use translated metadata service
const {
  connectorTypes: metadataConnectorTypes,
  directions,
  authTypes: metadataAuthTypes,
  fileFormats: metadataFileFormats,
  httpMethods: metadataHttpMethods,
  getProvidersForType
} = useTranslatedMetadata();

// Map to component format for compatibility
const connectorTypes = computed(() => 
  metadataConnectorTypes.value.map(type => ({
    title: type.label,
    value: type.value
  }))
);

const directionOptions = computed(() => 
  directions.value.map(dir => ({
    title: dir.label,
    value: dir.value,
    icon: dir.icon
  }))
);

const authTypes = computed(() => 
  metadataAuthTypes.value.map(auth => auth.label)
);

const fileFormats = computed(() => 
  metadataFileFormats.value.map(format => format.value)
);

const httpMethods = computed(() => {
  // Return all methods for endpoints configuration
  return metadataHttpMethods.value.map(m => m.value);
});

const providerOptions = computed(() => {
  return getProvidersForType(props.connector.type);
});

const showWriteConfigStep = computed(() => {
  return props.connector.direction === 'destination' || props.connector.direction === 'both';
});

// Watch for direction changes and ensure writeConfig exists
watch(() => props.connector.direction, (newDirection) => {
  if ((newDirection === 'destination' || newDirection === 'both') && !props.connector.config.writeConfig) {
    ensureWriteConfig();
  }
}, { immediate: true });

// Watch for type changes and ensure writeConfig exists if needed
watch(() => props.connector.type, () => {
  if (showWriteConfigStep.value && !props.connector.config.writeConfig) {
    ensureWriteConfig();
  }
}, { immediate: true });

// Watch for provider changes and ensure config is properly initialized
watch(() => props.connector.provider, (newProvider) => {
  if (newProvider && props.connector.type === 'Database') {
    ensureDatabaseConfig(newProvider);
  }
}, { immediate: true });

function ensureWriteConfig() {
  if (!props.connector.config.writeConfig) {
    if (props.connector.type === 'Database') {
      props.connector.config.writeConfig = {
        tableName: '',
        operation: 'INSERT',
        primaryKeys: [],
        batchSize: 1000
      };
    } else if (props.connector.type === 'File') {
      props.connector.config.writeConfig = {
        writeMode: 'OVERWRITE',
        includeHeaders: true,
        columnOrder: [],
        filenamePattern: '',
        sheetName: 'Sheet1',
        startCell: 'A1',
        structure: 'ARRAY',
        rootKey: null
      };
    } else if (props.connector.type === 'API') {
      props.connector.config.writeConfig = {
        requestFormat: 'JSON',
        wrapInArray: false,
        rootKey: null,
        batchSize: 100
      };
    }
  }
}

function ensureDatabaseConfig(provider) {
  if (provider === 'Snowflake') {
    // Ensure Snowflake-specific fields exist
    if (!props.connector.config.account) props.connector.config.account = '';
    if (!props.connector.config.warehouse) props.connector.config.warehouse = '';
    if (!props.connector.config.schema) props.connector.config.schema = '';
    if (!props.connector.config.role) props.connector.config.role = '';
    
    // Remove fields not used by Snowflake
    delete props.connector.config.host;
    delete props.connector.config.port;
    delete props.connector.config.useSsl;
    delete props.connector.config.projectId;
    delete props.connector.config.datasetId;
    delete props.connector.config.jsonCredentials;
  } else if (provider === 'BigQuery') {
    // Ensure BigQuery-specific fields exist
    if (!props.connector.config.projectId) props.connector.config.projectId = '';
    if (!props.connector.config.datasetId) props.connector.config.datasetId = '';
    if (!props.connector.config.location) props.connector.config.location = '';
    
    // Remove fields not used by BigQuery
    delete props.connector.config.host;
    delete props.connector.config.port;
    delete props.connector.config.database;
    delete props.connector.config.username;
    delete props.connector.config.password;
    delete props.connector.config.useSsl;
    delete props.connector.config.account;
    delete props.connector.config.warehouse;
    delete props.connector.config.schema;
    delete props.connector.config.schema;
    delete props.connector.config.role;
    
    // Ensure table name is required if destination
    if ((props.connector.direction === 'destination' || props.connector.direction === 'both') && 
        props.connector.config.writeConfig && !props.connector.config.writeConfig.tableName) {
      // Logic for mandatory table name is mostly handled by validateWriteConfig
    }
  } else if (provider === 'MongoDb') {
    // Ensure MongoDB-specific fields exist
    if (!props.connector.config.connectionString) props.connector.config.connectionString = '';
    if (!props.connector.config.database) props.connector.config.database = '';
    if (!props.connector.config.collectionName) props.connector.config.collectionName = '';
    if (!props.connector.config.filterJson) props.connector.config.filterJson = '';

    // Remove fields not used by MongoDB
    delete props.connector.config.host;
    delete props.connector.config.port;
    delete props.connector.config.username;
    delete props.connector.config.password;
    delete props.connector.config.useSsl;
    delete props.connector.config.account;
    delete props.connector.config.warehouse;
    delete props.connector.config.schema;
    delete props.connector.config.role;
    delete props.connector.config.projectId;
    delete props.connector.config.datasetId;
    delete props.connector.config.location;
    delete props.connector.config.jsonCredentials;
  } else if (provider === 'CosmosDb') {
    // Ensure Cosmos DB-specific fields exist
    if (!props.connector.config.cosmosEndpoint) props.connector.config.cosmosEndpoint = '';
    if (!props.connector.config.cosmosKey) props.connector.config.cosmosKey = '';
    if (!props.connector.config.database) props.connector.config.database = '';
    if (!props.connector.config.container) props.connector.config.container = '';
    
    // Remove fields not used by Cosmos DB
    delete props.connector.config.host;
    delete props.connector.config.port;
    delete props.connector.config.username;
    delete props.connector.config.password;
    delete props.connector.config.useSsl;
    delete props.connector.config.account;
    delete props.connector.config.warehouse;
    delete props.connector.config.schema;
    delete props.connector.config.role;
    delete props.connector.config.projectId;
    delete props.connector.config.datasetId;
    delete props.connector.config.location;
    delete props.connector.config.jsonCredentials;
    delete props.connector.config.connectionString;
    delete props.connector.config.collectionName;
    delete props.connector.config.filterJson;
  } else {
    // Ensure traditional database fields exist for other providers
    if (!props.connector.config.host) props.connector.config.host = '';
    if (!props.connector.config.port) props.connector.config.port = getDefaultPort(provider);
    if (!props.connector.config.useSsl) props.connector.config.useSsl = false;
    
    // Remove Snowflake/BigQuery-specific fields
    delete props.connector.config.account;
    delete props.connector.config.warehouse;
    delete props.connector.config.schema;
    delete props.connector.config.role;
    delete props.connector.config.projectId;
    delete props.connector.config.datasetId;
    delete props.connector.config.location;
    delete props.connector.config.jsonCredentials;
  }
}

const canProceed = computed(() => {
  switch (currentStep.value) {
    case 1:
      return !!props.connector.name && !!props.connector.type && !!props.connector.provider;
    case 2:
      return validateConnectionConfig();
    case 3:
      return schemaValidation.value.isValid && props.connector.schema.fields.length > 0;
    case 4:
      if (showWriteConfigStep.value) {
        return validateWriteConfig();
      }
      return true; // Review step
    case 5:
      return true; // Review step when write config is shown
    default:
      return false;
  }
});

const canSave = computed(() => {
  const baseValid = props.connector.name &&
         props.connector.type &&
         props.connector.provider &&
         validateConnectionConfig() &&
         schemaValidation.value.isValid &&
         props.connector.schema.fields.length > 0;
  
  if (showWriteConfigStep.value) {
    return baseValid && validateWriteConfig();
  }
  
  return baseValid;
});

function getTypeIcon(type) {
  const icons = {
    Database: 'mdi-database',
    API: 'mdi-api',
    File: 'mdi-file'
  };
  return icons[type] || 'mdi-help-circle';
}

function getDirectionIcon(direction) {
  const icons = {
    source: 'mdi-download',
    destination: 'mdi-upload',
    both: 'mdi-swap-horizontal'
  };
  return icons[direction] || 'mdi-help-circle';
}

function getDirectionLabel(direction) {
  const labels = {
    source: t('connectors.sourceOnly'),
    destination: t('connectors.destinationOnly'),
    both: t('connectors.both')
  };
  return labels[direction] || direction;
}

function getMethodColor(method) {
  const colors = {
    GET: 'blue',
    POST: 'green',
    PUT: 'orange',
    PATCH: 'purple',
    DELETE: 'red'
  };
  return colors[method] || 'grey';
}

function getDefaultPort(provider) {
  const ports = {
    'SQL Server': 1433,
    'SqlServer': 1433,
    'PostgreSQL': 5432,
    'MySQL': 3306,
    'Oracle': 1521,
    'Snowflake': '', // Snowflake doesn't use traditional ports
    'BigQuery': '', // BigQuery doesn't use traditional ports
    'Redshift': 5439
  };
  return ports[provider] || '';
}

function isValidJson(str) {
  try {
    JSON.parse(str);
    return true;
  } catch (e) {
    return false;
  }
}

function handleTypeChange() {
  props.connector.provider = '';
  props.connector.config = getDefaultConfig(props.connector.type);
}

function getDefaultConfig(type) {
  switch (type) {
    case 'Database':
      return {
        host: '',
        port: null,
        database: '',
        username: '',
        password: '',
        useSsl: false,
        useCustomConnectionString: false,
        connectionString: null,
        writeConfig: {
          tableName: '',
          operation: 'INSERT',
          primaryKeys: [],
          batchSize: 1000
        }
      };
    case 'API':
      return {
        baseUrl: '',
        authType: 'None',
        authToken: null,
        useDynamicToken: false,
        tokenEndpointUrl: null,
        tokenEndpointMethod: 'POST',
        tokenEndpointBody: null,
        tokenEndpointHeadersText: null,
        tokenResponsePath: 'access_token',
        tokenExpirySeconds: null,
        apiKeyHeader: null,
        apiKeyValue: null,
        username: null,
        password: null,
        headers: {},
        queryParameters: {},
        timeoutSeconds: 30,
        endpoints: [],
        writeConfig: {
          requestFormat: 'JSON',
          wrapInArray: false,
          rootKey: null,
          batchSize: 100
        }
      };
    case 'File':
      return {
        format: 'CSV',
        path: '',
        delimiter: ',',
        hasHeader: true,
        sheetName: null,
        encoding: 'UTF-8',
        // FTP fields
        ftpHost: null,
        ftpPort: 21,
        ftpUsername: null,
        ftpPassword: null,
        // SFTP fields
        sftpHost: null,
        sftpPort: 22,
        sftpUsername: null,
        sftpPassword: null,
        // S3 fields
        s3Bucket: null,
        s3Region: null,
        s3AccessKey: null,
        s3SecretKey: null,
        s3Endpoint: null,
        // AzureBlob fields
        azureAccountName: null,
        azureContainer: null,
        azureAccountKey: null,
        // GCS fields
        gcsBucket: null,
        gcsProjectId: null,
        gcsJsonCredentials: null,
        writeConfig: {
          writeMode: 'OVERWRITE',
          includeHeaders: true,
          columnOrder: [],
          filenamePattern: '',
          sheetName: 'Sheet1',
          startCell: 'A1',
          structure: 'ARRAY',
          rootKey: null
        }
      };
    default:
      return {};
  }
}

function validateConnectionConfig() {
  const { type, provider, config } = props.connector;
  
  if (type === 'Database') {
    if (config.useCustomConnectionString) {
      return !!config.connectionString;
    }
    
    // Snowflake-specific validation
    if (provider === 'Snowflake') {
      return !!config.account && !!config.warehouse && !!config.database && 
             !!config.schema && !!config.username && !!config.password;
    }
    
    // BigQuery-specific validation
    if (provider === 'BigQuery') {
      const basicValid = !!config.projectId && !!config.datasetId;
      if (config.jsonCredentials) {
        return basicValid && isValidJson(config.jsonCredentials);
      }
      return basicValid;
    }
    
    // MongoDB-specific validation
    if (provider === 'MongoDb') {
      const basicMongoValid = !!config.connectionString && !!config.database && !!config.collectionName;
      if (config.filterJson) {
        return basicMongoValid && isValidJson(config.filterJson);
      }
      return basicMongoValid;
    }
    
    // Cosmos DB-specific validation
    if (provider === 'CosmosDb') {
      return !!config.cosmosEndpoint && !!config.cosmosKey && !!config.database && !!config.container;
    }
    
    // Other database providers
    return !!config.host && !!config.database && !!config.username && !!config.password;
  }
  
  if (type === 'API') {
    return !!config.baseUrl;
  }
  
  if (type === 'File') {
    const basicValid = !!config.path;
    if (provider === 'GCS') {
      const gcsValid = !!config.gcsBucket && !!config.gcsProjectId;
      if (config.gcsJsonCredentials) {
        return gcsValid && basicValid && isValidJson(config.gcsJsonCredentials);
      }
      return gcsValid && basicValid;
    }
    return basicValid;
  }
  
  return false;
}

function validateWriteConfig() {
  const { type, config } = props.connector;
  
  if (!config.writeConfig) return false;
  
  if (type === 'Database') {
    // Table name is required
    if (!config.writeConfig.tableName) return false;
    
    // For UPDATE/UPSERT operations, primary keys are required
    if (['UPDATE', 'UPSERT'].includes(config.writeConfig.operation)) {
      return config.writeConfig.primaryKeys && config.writeConfig.primaryKeys.length > 0;
    }
    
    return true;
  }
  
  if (type === 'API') {
    // Request format is required
    return !!config.writeConfig.requestFormat;
  }
  
  if (type === 'File') {
    // Write mode is required
    if (!config.writeConfig.writeMode) return false;
    
    // For CSV, column order should be defined (can be empty initially)
    if (config.format === 'CSV') {
      return config.writeConfig.columnOrder !== undefined;
    }
    
    // For Excel, sheet name is required
    if (config.format === 'Excel') {
      return !!config.writeConfig.sheetName;
    }
    
    // For JSON, structure is required
    if (config.format === 'JSON') {
      return !!config.writeConfig.structure;
    }
    
    return true;
  }
  
  return false;
}

function handleSchemaValidation(validation) {
  schemaValidation.value = validation;
}

async function testConnectionBeforeSave() {
  testingConnection.value = true;
  connectionTestResult.value = false;
  
  try {
    const { testConnection } = await import('@/services/connectorService');
    const result = await testConnection(props.connector);
    
    connectionTestSuccess.value = result.success;
    connectionTestMessage.value = result.message;
    connectionTestResult.value = true;
  } catch (error) {
    console.error('Error testing connection:', error);
    connectionTestSuccess.value = false;
    const errorMessage = error.response?.data?.message || error.response?.data?.title || error.message;
    connectionTestMessage.value = `Connection test failed: ${errorMessage}`;
    connectionTestResult.value = true;
  } finally {
    testingConnection.value = false;
  }
}

function parseHeadersFromText(headersText) {
  if (!headersText) return {};
  
  try {
    return JSON.parse(headersText);
  } catch (e) {
    // If not valid JSON, treat as key:value pairs
    const headers = {};
    headersText.split('\n').forEach(line => {
      const [key, ...valueParts] = line.split(':');
      if (key && valueParts.length > 0) {
        headers[key.trim()] = valueParts.join(':').trim();
      }
    });
    return headers;
  }
}

async function handleSave() {
  if (!canSave.value) return;
  
  saving.value = true;
  try {
    // Parse and set custom headers
    if (props.connector.type === 'API') {
      props.connector.config.headers = parseHeadersFromText(customHeadersText.value);
      
      // Parse and set token endpoint headers if using dynamic tokens
      if (props.connector.config.useDynamicToken) {
        props.connector.config.tokenEndpointHeaders = parseHeadersFromText(props.connector.config.tokenEndpointHeadersText);
      }
    }
    
    emit('save', props.connector);
  } finally {
    saving.value = false;
  }
}

// Initialize text fields from connector config on load
if (props.connector.config?.headers && typeof props.connector.config.headers === 'object') {
  customHeadersText.value = JSON.stringify(props.connector.config.headers, null, 2);
}

if (props.connector.config?.tokenEndpointHeaders && typeof props.connector.config.tokenEndpointHeaders === 'object') {
  if (!props.connector.config.tokenEndpointHeadersText) {
    props.connector.config.tokenEndpointHeadersText = JSON.stringify(props.connector.config.tokenEndpointHeaders, null, 2);
  }
}

// Initialize config if empty
watch(() => props.connector.type, (newType) => {
  if (newType && !props.connector.config) {
    props.connector.config = getDefaultConfig(newType);
  }
}, { immediate: true });
</script>

<style scoped>
.wizard-card {
  display: flex;
  flex-direction: column;
}

.wizard-stepper {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.wizard-stepper :deep(.v-stepper-header) {
  flex-shrink: 0;
  padding: 24px 16px;
  background: rgba(var(--v-theme-surface-variant), 0.3);
}

/* Style stepper items */
.wizard-stepper :deep(.v-stepper-item) {
  padding: 12px 16px;
}

.wizard-stepper :deep(.v-stepper-item__avatar) {
  margin-bottom: 8px;
  width: 40px;
  height: 40px;
  font-size: 18px;
  font-weight: 600;
  border: 2px solid rgba(var(--v-theme-primary), 0.3);
  background: rgba(var(--v-theme-surface), 1);
}

.wizard-stepper :deep(.v-stepper-item--selected .v-stepper-item__avatar) {
  background: rgb(var(--v-theme-primary));
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 4px 8px rgba(var(--v-theme-primary), 0.3);
  transform: scale(1.1);
  transition: all 0.3s ease;
}

.wizard-stepper :deep(.v-stepper-item--complete .v-stepper-item__avatar) {
  background: rgb(var(--v-theme-success));
  border-color: rgb(var(--v-theme-success));
}

.wizard-stepper :deep(.v-stepper-item__title) {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
}

.wizard-stepper :deep(.v-stepper-item--selected .v-stepper-item__title) {
  color: rgb(var(--v-theme-primary));
  font-weight: 700;
}

.wizard-stepper :deep(.v-stepper-item__subtitle) {
  font-size: 12px;
  opacity: 0.7;
}

/* Step arrow between items */
.step-arrow {
  color: rgba(var(--v-theme-primary), 0.5);
  font-size: 28px;
  margin: 0 16px;
  align-self: center;
  margin-top: 20px;
}

.stepper-window {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.stepper-window :deep(.v-stepper-window-item) {
  background: transparent !important;
}


</style>
