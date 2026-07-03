using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Application.Common.Pagination;
using OrdersService.Application.Orders.Abstractions;
using OrdersService.Application.Orders.Contracts;
using Microsoft.AspNetCore.Authorization;
using OrdersService.Api.Security;

namespace OrdersService.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/orders")]
[Produces("application/json")]
public sealed class OrdersController(IOrdersService ordersService) : ControllerBase
{
    private const string GetOrderByIdRouteName = "Orders_GetById";

    private readonly IOrdersService _ordersService = ordersService;

    [HttpPost]
    [MapToApiVersion(1.0)]
    [Authorize(Policy = AuthorizationPolicyNames.CanCreateOrder)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> CreateAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _ordersService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtRoute(
            GetOrderByIdRouteName,
            new
            {
                version = "1",
                id = order.Id
            },
            order);
    }

    [HttpGet("{id:guid}", Name = GetOrderByIdRouteName)]
    [MapToApiVersion(1.0)]
    [Authorize(Policy = AuthorizationPolicyNames.AuthenticatedUser)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _ordersService.GetByIdAsync(
            id,
            cancellationToken);

        if (order is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order was not found.",
                detail: $"Order with ID '{id}' was not found.");
        }

        return Ok(order);
    }

    [HttpGet]
    [MapToApiVersion(1.0)]
    [Authorize(Policy = AuthorizationPolicyNames.SupportOrAdmin)]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> ListAsync(
        [FromQuery] ListOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ordersService.ListAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}