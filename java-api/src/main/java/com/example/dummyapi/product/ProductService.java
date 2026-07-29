package com.example.dummyapi.product;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.product.dto.ProductDtos.ProductRequest;
import com.example.dummyapi.product.dto.ProductDtos.ProductResponse;
import com.example.dummyapi.product.dto.ProductDtos.StockAdjustmentRequest;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
public class ProductService {

    private final ProductRepository productRepository;

    // Config-driven business rule: the low-stock threshold is not hardcoded,
    // it comes from application.yml (app.products.low-stock-threshold).
    @Value("${app.products.low-stock-threshold}")
    private int lowStockThreshold;

    public ProductService(ProductRepository productRepository) {
        this.productRepository = productRepository;
    }

    @Transactional
    public ProductResponse create(ProductRequest request) {
        if (productRepository.existsBySku(request.sku())) {
            throw ApiException.conflict("DUPLICATE_SKU", "A product with this SKU already exists");
        }
        Product product = new Product(
                request.sku(), request.name(), request.description(),
                request.price(), request.stockQuantity()
        );
        product.setLowStockThreshold(lowStockThreshold);
        productRepository.save(product);
        return toResponse(product);
    }

    public ProductResponse getById(Long id) {
        return toResponse(findOrThrow(id));
    }

    public List<ProductResponse> listAll() {
        return productRepository.findAll().stream().map(this::toResponse).toList();
    }

    @Transactional
    public ProductResponse adjustStock(Long id, StockAdjustmentRequest request) {
        Product product = findOrThrow(id);
        int resultingStock = product.getStockQuantity() + request.delta();
        if (resultingStock < 0) {
            throw ApiException.conflict("INSUFFICIENT_STOCK",
                    "Stock adjustment would result in a negative quantity for product " + product.getSku());
        }
        if (request.delta() >= 0) {
            product.increaseStock(request.delta());
        } else {
            product.decreaseStock(-request.delta());
        }
        return toResponse(product);
    }

    /** Public: used by OrderService (a different package) to reserve/restock within a transaction. */
    public Product findOrThrow(Long id) {
        Product product = productRepository.findById(id)
                .orElseThrow(() -> ApiException.notFound("PRODUCT_NOT_FOUND", "Product not found: " + id));
        product.setLowStockThreshold(lowStockThreshold);
        return product;
    }

    private ProductResponse toResponse(Product product) {
        return new ProductResponse(
                product.getId(), product.getSku(), product.getName(), product.getDescription(),
                product.getPrice(), product.getStockQuantity(), product.isLowStock()
        );
    }
}
