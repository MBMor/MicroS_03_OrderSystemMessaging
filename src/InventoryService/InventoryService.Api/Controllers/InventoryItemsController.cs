using Asp.Versioning;
using InventoryService.Application.Common.Pagination;
using InventoryService.Application.InventoryItems.Abstractions;
using InventoryService.Application.InventoryItems.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inventory-items")]
[Produces("application/json")]
public sealed class InventoryItemsController : ControllerBase
{
    private readonly IInventoryItemsService _inventoryItemsService;

    public InventoryItemsController(IInventoryItemsService inventoryItemsService)
    {
        _inventoryItemsService = inventoryItemsService;
    }

    [HttpPost]
    [MapToApiVersion(1.0)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryItemResponse>> CreateAsync(
        [FromBody] CreateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var inventoryItem = await _inventoryItemsService.CreateAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetByProductIdAsync),
            new
            {
                version = "1",
                productId = inventoryItem.ProductId
            },
            inventoryItem);
    }

    [HttpGet("{productId:guid}")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItemResponse>> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var inventoryItem = await _inventoryItemsService.GetByProductIdAsync(
            productId,
            cancellationToken);

        if (inventoryItem is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inventory item was not found.",
                detail: $"Inventory item for product ID '{productId}' was not found.");
        }

        return Ok(inventoryItem);
    }

    [HttpGet]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(PagedResult<InventoryItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<InventoryItemResponse>>> ListAsync(
        [FromQuery] ListInventoryItemsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryItemsService.ListAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{productId:guid}")]
    [MapToApiVersion(1.0)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItemResponse>> UpdateAsync(
        Guid productId,
        [FromBody] UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var inventoryItem = await _inventoryItemsService.UpdateAsync(
            productId,
            request,
            cancellationToken);

        return Ok(inventoryItem);
    }
}