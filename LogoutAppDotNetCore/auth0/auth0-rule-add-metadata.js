async function rule(user, context, callback) {

    var getClientsData = async function (useCache) {
  
          const validTill = configuration.validTill;
          if (validTill && validTill > Date.now() && configuration.clientsData && useCache) {
              console.log("Cache was hit. Getting data!");
              return JSON.parse(configuration.clientsData);
          }
          else {
              console.log("Use Cache flag set to: " + useCache);
              console.log("cache miss!");
              console.log("fecth from mgmt api and add to configuration object for 10-15 minutes!");
  
              const rp = require("request-promise");
              const ManagementClient = require('auth0@3.0.1').ManagementClient;
  
              const auth0LoginOpts = {
                  url: configuration.MANAGEMENT_TOKEN_URL,
                  method: "POST",
                  json: true,
                  body: {
                      grant_type: "client_credentials",
                      client_id: configuration.MGMT_CLIENT_ID,
                      client_secret: configuration.MGMT_CLIENT_SECRET,
                      audience: configuration.MANAGEMENT_API_AUDIENCE
                  }
              };
  
              const auth0LoginBody = await rp(auth0LoginOpts);
              //console.log(auth0LoginBody);
              let management = new ManagementClient({
                  token: auth0LoginBody.access_token,
                  domain: configuration.AUTH0_DOMAIN,
                  retry: {
                      enabled: true
                  }
              });

              const clientsDummy = await management.clients.getAll({ fields: "client_id", include_fields: true, app_type: "regular_web,spa", page: 0, per_page: 1, include_totals: true });
              //console.log(clientsDummy);
              var total = clientsDummy.total;
              //console.log(total);
              var pages = Math.ceil(total/100);
              var clients = [];
              for (let index = 0; index < pages; index++) {
                const fetch = await management.clients.getAll({ fields: "client_id,name,client_metadata,app_type,addons", include_fields: true, app_type: "regular_web,spa", page:index, per_page: 100, include_totals: false });
                //console.log(fetch);
                //console.log(fetch.length);
                clients.push(...fetch);
                
              }

              console.log(`Found ${clients.length} apps`);

              //filter only for wsfed and samlp for now
              var clientsToStore = clients.filter( client => {
                  return client.addons && ( client.addons.samlp || client.addons.wsfed);
              });
              
              console.log(`Found ${clientsToStore.length} wsfed and samlp apps`);

              await management.setRulesConfig({ key: "clientsData" }, { value: JSON.stringify(clientsToStore) });
              await management.setRulesConfig({ key: "validTill" }, { value: (Date.now() + 1000 * 600).toString() });
              return clientsToStore;
  
          }
  
      };
      var _ = require('lodash');
  
      if (context.clientID === "<client_id_of_logout_app>" && context.sso.current_clients && context.sso.current_clients.length > 0) {
  
          var clientsData = await getClientsData(true);
          var foundAll = true;

          for (const client of context.sso.current_clients)
          {
     
            var match = clientsData.find(item => {
                return item.client_id === client;
            });
            if(typeof match === 'undefined') 
            { 
                foundAll = false;
                 break;
            }
          }
          console.log("Were all clients from SSO session found in the cache? : " + foundAll);

          if(!foundAll) {
            console.log("A client particiapting in the SSO did not have data in the cache, reload cache and move on");
          clientsData = await getClientsData(false);
          }

          var clientsMetadata = {};
 						context.sso.current_clients.forEach((client) => {
            var match = clientsData.find(item => {
                return item.client_id === client;
            });
            if (match) {
                //console.log(match);
                let { name, client_metadata: { logout_url, appType } } = match;
                clientsMetadata[client] = { name, logout_url, appType };
            }

        });
          context.idToken.current_clients_alt = clientsMetadata;
  
      }
      return callback(null, user, context);
  }
  
  
