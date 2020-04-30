using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GreenPipes;
using MassTransit;
using MassTransit.Courier;
using MassTransit.Courier.Contracts;
using MasstransitTest.Proxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MasstransitTest
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks();
            services.AddControllers();
            services.AddMassTransit(x =>
            {
                x.AddBus(provider => Bus.Factory.CreateUsingInMemory(new Uri("loopback://localhost/"), cfg =>
                {
                    cfg.UseInMemoryScheduler();

                    cfg.UseHealthCheck(provider);

                    cfg.ReceiveEndpoint("DeductStock", ep =>
                    {
                        //PrefetchCount �� in memory bus ����Ч 
                        ep.ExecuteActivityHost<DeductStockActivity, DeductStockModel>(new Uri("loopback://localhost/DeductStock-Compensate"));
                    });

                    cfg.ReceiveEndpoint("DeductStock-Compensate", ep =>
                    {
                        ep.CompensateActivityHost<DeductStockActivity, DeductStockLog>(conf=> {
                            conf.UseRetry(policy =>
                            {
                                policy.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
                            });
                        });
                        ep.UseScheduledRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
                    });
                    cfg.ReceiveEndpoint("DeductBalance", ep =>
                    {
                        ep.ExecuteActivityHost<DeductBalanceActivity, DeductBalanceModel>(new Uri("loopback://localhost/DeductBalance-Compensate"));
                    });

                    cfg.ReceiveEndpoint("DeductBalance-Compensate", ep =>
                    {
                        ep.CompensateActivityHost<DeductBalanceActivity, DeductBalanceLog>(conf => {
                            conf.UseRetry(policy =>
                            {
                                policy.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
                            });
                        });
                        ep.UseScheduledRedelivery(r => r.Intervals(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
                    });

                    cfg.ReceiveEndpoint("CreateOrder", ep =>
                    {
                        ep.ExecuteActivityHost<CreateOrderActivity, CreateOrderModel>(new Uri("loopback://localhost/CreateOrder-Compensate"));
                    });

                    cfg.ReceiveEndpoint("Activity-Request", ep =>
                    {
                        var requestProxy = new CreateOrderRequestProxy();
                        var responseProxy = new CreateOrderResponseProxy();
                        ep.Instance(requestProxy);
                        ep.Instance(responseProxy);
                    });
                    cfg.ConfigureEndpoints(provider);
                    
                }));
                x.AddRequestClient<CreateOrderCommand>();

            });
            services.AddMassTransitHostedService();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}